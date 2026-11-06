using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class SelectPresenter : IDisposable {
        const string LOG_PREFIX = "[SelectPresenter]";

        enum SceneInputState {
            Selecting,
            ReadyToStart,
            Dialog,
            TransitioningToScreen,
            TransitioningToStageSelect,
        }

        readonly SelectScene view;
        readonly ISceneTransitionService transitionService;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IInputFocusService inputFocus;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly ITutorialSetting tutorialSetting;
        readonly IMatchingModel matchingModel;
        readonly IMatchingDuelOperations duelOperations;
        readonly PlayerSession playerSession;
        readonly IPlayerProfileService profiles;
        readonly TrialController trial;
        readonly ChoiceDialogPresenter cpuDialog;
        readonly ChoiceDialogPresenter introductionDialog;
        readonly ITrialProgressService trialProgress;
        readonly CompositeDisposable subscriptions = new();
        readonly CharacterSelectSelectionPolicy selectionPolicy = new();
        readonly List<CharacterSelectSlotState> slotStates = new();
        bool showingCharacterIntro;
        bool initialized;
        bool disposed;
        bool startTransitionInputEnabled;
        bool eastStartConfirmationArmed;
        bool isOnlineMatchmakingInProgress;
        bool wasReadyToStart;
        bool onlineScenePresenceReady;
        bool hasPublishedOnlineStatus;
        OnlineDuelPlayerStatus lastPublishedOnlineStatus;
        SceneInputState inputState = SceneInputState.Selecting;

        [Inject]
        public SelectPresenter(
            SelectScene view,
            ISceneTransitionService transitionService,
            IGamePadRegistry gamePadRegistry,
            IBattleSelectSetting battleSelectSetting,
            IPlayerSelectSetting playerSelectSetting,
            IAppStrikerRegistry appStrikerRegistry,
            IAppNetworkSetting appNetworkSetting,
            ITutorialSetting tutorialSetting,
            IMatchingModel matchingModel,
            IMatchingDuelOperations duelOperations, PlayerSession playerSession, IPlayerProfileService profiles, TrialController trial, IInputFocusService inputFocus, ITrialProgressService trialProgress, IAudioSetting audioSetting) {
            this.view = view;
            this.transitionService = transitionService;
            this.gamePadRegistry = gamePadRegistry;
            this.inputFocus = inputFocus;
            this.battleSelectSetting = battleSelectSetting;
            this.playerSelectSetting = playerSelectSetting;
            this.appStrikerRegistry = appStrikerRegistry;
            this.appNetworkSetting = appNetworkSetting;
            this.tutorialSetting = tutorialSetting;
            this.matchingModel = matchingModel;
            this.duelOperations = duelOperations;
            this.playerSession = playerSession; this.profiles = profiles; this.trial = trial;
            inputFocus.RegisterModal(view.CharacterIntroSlideshow.ModalRoot).AddTo(subscriptions);
            inputFocus.RegisterModal(view.StartButtonAnimation.WaitingRoot).AddTo(subscriptions);
            gamePadRegistry.OnAnyButtonDown.Where(e => inputFocus.CanReceive(view.CharacterIntroSlideshow.ModalRoot, e.PlayerId) && e.Button == GamePadButton.East).Subscribe(_ => view.CharacterIntroSlideshow.Advance()).AddTo(subscriptions);
            gamePadRegistry.OnAnyButtonDown.Where(e => inputFocus.CanReceive(view.StartButtonAnimation.WaitingRoot, e.PlayerId) && e.Button == GamePadButton.South).Subscribe(_ => duelOperations.CancelMatchmaking()).AddTo(subscriptions);
            cpuDialog = new ChoiceDialogPresenter(view.CpuDialog, gamePadRegistry, inputFocus);
            this.trialProgress = trialProgress;
            introductionDialog = new ChoiceDialogPresenter(view.TrialIntroduction.Dialog, gamePadRegistry, inputFocus);
            audioSetting.VolumeBalance.Subscribe(volume => view.TrialIntroduction.SetVolume(volume.MasterVolume * volume.SeVolume)).AddTo(subscriptions);

            _ = InitializeAfterFrameAsync();
        }

        async Awaitable InitializeAfterFrameAsync() {
            await Task.Yield();
            Debug.Log($"{LOG_PREFIX} InitializeAfterFrameAsync resumed and will initialize");
            await InitializeAsync();
        }

        async Awaitable InitializeAsync() {
            if (initialized) {
                Debug.Log($"{LOG_PREFIX} Initialize skipped because already initialized");
                return;
            }

            Debug.Log($"{LOG_PREFIX} Initialize start. buttonCount={view.CharacterSelectButtons.Length}");

            selectionPolicy.Reset(playerSelectSetting);

            foreach (var button in view.CharacterSelectButtons) {
                button.SetInteractable(CanSelectStriker(button.Striker));
                button.OnStrikerClicked
                    .Subscribe(OnStrikerClicked)
                    .AddTo(subscriptions);
            }

            view.Backbutton.OnBackPressed
                .Subscribe(_ => RequestStageSelectTransition())
                .AddTo(subscriptions);

            view.StartButtonAnimation.OnStartRequested
                .Subscribe(unit => {
                    _ = RequestPlaySceneTransitionAsync(true);
                })
                .AddTo(subscriptions);

            playerSelectSetting.SelectedStrikers
                .Subscribe(_ => RefreshState())
                .AddTo(subscriptions);

            for (var playerId = 0; playerId < CharacterSelectSelectionPolicy.MAXPLAYERS; playerId++) {
                gamePadRegistry.Get(playerId).HasGamePad
                    .Subscribe(_ => RefreshState())
                    .AddTo(subscriptions);

                var inputSlot = playerId;
                gamePadRegistry.Get(playerId).OnButtonDown
                    .Where(_ => !playerSession.IsTrial || inputSlot == 0)
                    .Subscribe(button => OnButtonDown(button))
                    .AddTo(subscriptions);
            }

            RefreshState();
            _ = EnableStartInputAfterSceneEnterAsync();
            initialized = true;
            Debug.Log($"{LOG_PREFIX} Initialize completed");
        }

        async Awaitable EnableStartInputAfterSceneEnterAsync() {
            startTransitionInputEnabled = false;
            Debug.Log($"{LOG_PREFIX} EnableStartInputAfterSceneEnterAsync requesting end transition. scene={AppScene.CharacterSelect}");
            var result = await transitionService.RequestEndTransitionAsync(AppScene.CharacterSelect);
            if (disposed) return;
            if (result.IsSuccess && playerSession.IsTrial) await ShowTrialIntroductionAsync();
            if (disposed) return;
            startTransitionInputEnabled = true;
            onlineScenePresenceReady = true;
            PublishOnlineCharacterSelectStatus();
            Debug.Log($"{LOG_PREFIX} EnableStartInputAfterSceneEnterAsync completed. isSuccess={result.IsSuccess}, startTransitionInputEnabled={startTransitionInputEnabled}");
        }

        bool CanSelectStriker(Striker striker) => !playerSession.IsTrial || striker != Striker.DemonKing;

        async Task ShowTrialIntroductionAsync() {
            var name = playerSession.GetName(0);
            if (trialProgress.GetTrialProgress(name).introductionSeen || !string.IsNullOrEmpty(profiles.Get(name).lastTrialAt)) return;
            using var hold = inputFocus.HoldScreen();
            inputState = SceneInputState.Dialog;
            RefreshState();
            try {
                var pending = introductionDialog.AskAsync("チャレンジモードとは", "CPUと戦い、階級の頂点を目指すモード！\n最初はピアニッシモ級からスタート\n好きなストライカーで挑戦しよう", new[] { "挑戦する！" }, inputPlayerId: 0, directInput: true);
                view.TrialIntroduction.ShowPage();
                await pending;
                if (disposed) return;
                view.TrialIntroduction.StopNarration();
                trialProgress.CompleteTrialIntroduction(name);
            } catch (OperationCanceledException) {
                // Scene disposal cancels the dialog; incomplete guides remain available next time.
            } catch (Exception exception) {
                Debug.LogError($"{LOG_PREFIX} Challenge introduction could not be saved: {exception.Message}");
            } finally {
                if (!disposed) {
                    view.TrialIntroduction.StopNarration();
                    inputState = SceneInputState.Selecting;
                    RefreshState();
                }
            }
        }

        void OnStrikerClicked(StrikerClickRequest request) {
            if (playerSession.IsTrial && request.PlayerId != 0) return;
            if (IsTransitioning() || !CanSelectStriker(request.Striker)) {
                return;
            }

            var targetSlot = playerSession.IsTrial || tutorialSetting.IsTutorialBattleRequested || IsOnlineSelectionFlow()
                ? 0
                : selectionPolicy.ResolveSelectionTargetSlot(
                    request.PlayerId,
                    GetJoinedPlayerCount(),
                    playerSelectSetting.TryGetStriker(0, out _));

            playerSelectSetting.SelectStriker(targetSlot, request.Striker);
            selectionPolicy.RecordSelection(targetSlot);

            RefreshState();
        }

        void OnButtonDown(GamePadButton button) {
            if (showingCharacterIntro) {
                if (button == GamePadButton.East) {
                    view.CharacterIntroSlideshow.Advance();
                }
                return;
            }

            if (button == GamePadButton.South) {
                if (isOnlineMatchmakingInProgress) {
                    Debug.Log($"{LOG_PREFIX} OnButtonDown South received. cancel online matchmaking requested");
                    duelOperations.CancelMatchmaking();
                    return;
                }

                if (IsTransitioning()) {
                    return;
                }

                if (!TryUndoSelection()) {
                    view.Backbutton.PlayClickSound();
                    RequestStageSelectTransition();
                }
                return;
            }

            if (IsTransitioning()) {
                return;
            }

            if (button != GamePadButton.East) return;
            Debug.Log($"{LOG_PREFIX} OnButtonDown East received. play transition requested");
            if (inputState != SceneInputState.ReadyToStart) {
                Debug.Log($"{LOG_PREFIX} OnButtonDown East ignored because inputState is not ReadyToStart. inputState={inputState}");
                return;
            }

            if (eastStartConfirmationArmed) {
                eastStartConfirmationArmed = false;
                Debug.Log($"{LOG_PREFIX} OnButtonDown East consumed as first confirm press");
                return;
            }

            _ = RequestPlaySceneTransitionAsync(false);
        }

        async Awaitable RequestPlaySceneTransitionAsync(bool requireStartButtonReady) {
            if (IsTransitioning()) {
                Debug.Log($"{LOG_PREFIX} RequestPlaySceneTransition ignored because transitioning. inputState={inputState}");
                return;
            }

            if (inputState != SceneInputState.ReadyToStart) {
                Debug.Log($"{LOG_PREFIX} RequestPlaySceneTransition ignored because inputState is not ReadyToStart. inputState={inputState}");
                return;
            }

            if (!startTransitionInputEnabled) {
                Debug.Log($"{LOG_PREFIX} RequestPlaySceneTransition ignored because startTransitionInputEnabled=false");
                return;
            }

            if (requireStartButtonReady && !view.StartButtonAnimation.IsStartInputReady) {
                Debug.Log($"{LOG_PREFIX} RequestPlaySceneTransition ignored because StartButtonAnimation is not ready");
                return;
            }

            using var inputHold = inputFocus.HoldScreen();
            inputState = SceneInputState.Dialog;
            try {
                if (playerSession.IsTrial) trial.PrepareNext();
                else if (!IsOnlineSelectionFlow() && !tutorialSetting.IsTutorialBattleRequested && !await PrepareLocalParticipantsAsync()) return;
                if (disposed) return;
                if (tutorialSetting.IsTutorialBattleRequested && !await PrepareTutorialAsync()) return;
            } catch (OperationCanceledException) {
                if (!disposed) inputState = SceneInputState.ReadyToStart;
                return;
            } catch (Exception exception) {
                Debug.LogError($"{LOG_PREFIX} Battle preparation failed: {exception.Message}");
                if (!disposed) {
                    try { await cpuDialog.AskAsync("バトルを開始できませんでした", "名前・CPU設定の保存先と試練の設定を確認して、もう一度お試しください", new[] { "OK" }); }
                    catch (OperationCanceledException) { }
                }
                if (!disposed) inputState = SceneInputState.ReadyToStart;
                return;
            }
            inputState = SceneInputState.TransitioningToScreen;

            if (IsOnlineSelectionFlow()) {
                view.StartButtonAnimation.SetOnlineWaitingPopupVisible(true);
                isOnlineMatchmakingInProgress = true;
                RefreshStatusView();
                try {
                    Debug.Log($"{LOG_PREFIX} Online match start. stage={battleSelectSetting.SelectedStage.CurrentValue}, musicId={battleSelectSetting.SelectedMusicId.CurrentValue}");
                    await MatchOnlineAsync();
                    Debug.Log($"{LOG_PREFIX} Online match completed.");
                }
                catch (OperationCanceledException) {
                    Debug.Log($"{LOG_PREFIX} Online match canceled by player");
                    inputState = SceneInputState.ReadyToStart;
                    return;
                }
                catch (Exception exception) {
                    Debug.LogError($"{LOG_PREFIX} Online match failed: {exception.Message}");
                    Debug.LogException(exception);
                    inputState = SceneInputState.ReadyToStart;
                    return;
                }
                finally {
                    isOnlineMatchmakingInProgress = false;
                    RefreshStatusView();
                    view.StartButtonAnimation.SetOnlineWaitingPopupVisible(false);
                    Debug.Log($"{LOG_PREFIX} Online match cleanup. popupHidden=true");
                }
            }

            var nextScene = ResolvePlayScene();
            Debug.Log($"{LOG_PREFIX} RequestPlaySceneTransition calling RequestStartTransition. nextScene={nextScene}, wasOnlineFlow={IsOnlineSelectionFlow()}");
            var result = transitionService.RequestStartTransition(nextScene);
            if (!result.IsSuccess) {
                Debug.LogWarning($"{LOG_PREFIX} RequestPlaySceneTransition rejected. nextScene={nextScene}, isSuccess=false. Transition service was not Idle; see [SceneTransitionService] logs for the matching START request id.");
                inputState = SceneInputState.ReadyToStart;
                return;
            }

            Debug.Log($"{LOG_PREFIX} RequestPlaySceneTransition accepted. inputState={inputState}, nextScene={nextScene}, isSuccess=true");
            var mainCamera = Camera.main;
            if (mainCamera != null) {
                view.ClickSound.PlayAtApp(mainCamera.transform.position);
            }
        }

        async Task<bool> PrepareLocalParticipantsAsync() {
            var humanSlots = new[] { gamePadRegistry.Get(0).HasGamePad.CurrentValue, gamePadRegistry.Get(1).HasGamePad.CurrentValue };
            if (!humanSlots[0] || !humanSlots[1]) {
                var cpuSlot = humanSlots[1] ? 0 : 1;
                if (!playerSelectSetting.TryGetStriker(cpuSlot, out var cpuStriker)) {
                    throw new InvalidOperationException($"Player {cpuSlot} has no selected striker.");
                }
                var grades = Enumerable.Range(1, 4).Select(level => PlayerGrades.Cpu(level, cpuStriker)).ToArray();
                var choice = await cpuDialog.SelectValueAsync("CPUの階級", grades, profiles.CpuLevel - 1);
                if (disposed) return false;
                if (!choice.HasValue) { inputState = SceneInputState.ReadyToStart; return false; }
                var level = choice.Value + 1;
                if (level != profiles.CpuLevel) profiles.CpuLevel = level;
                battleSelectSetting.SetAiStrength(level);
            }
            for (var slot = 0; slot < humanSlots.Length; slot++) {
                if (!humanSlots[slot]) continue;
                while (string.IsNullOrWhiteSpace(playerSession.GetName(slot))) {
                    var choice = await cpuDialog.AskAsync($"{slot + 1}P 名前入力", "", new[] { "確定", "戻る" }, "", inputPlayerId: slot, directInput: true);
                    if (disposed) return false;
                    if (choice == 1) { inputState = SceneInputState.ReadyToStart; return false; }
                    if (!PlayerProfileService.TryNormalizeName(PlayerProfileService.EnteredNameOrGuest(cpuDialog.InputText), out var name)) continue;
                    await profiles.SelectNameAsync(slot, name);
                    if (disposed) return false;
                }
            }
            for (var slot = 0; slot < humanSlots.Length; slot++) {
                if (humanSlots[slot] == gamePadRegistry.Get(slot).HasGamePad.CurrentValue) continue;
                inputState = SceneInputState.ReadyToStart;
                return false;
            }
            var identities = new PlayerIdentity[2];
            for (var slot = 0; slot < identities.Length; slot++) identities[slot] = humanSlots[slot]
                ? profiles.Get(playerSession.GetName(slot)).Identity : CreateCpuIdentity(slot, battleSelectSetting.AiStrength);
            playerSession.SetMode(BattlePlayMode.Local);
            playerSession.Prepare(identities);
            return true;
        }

        PlayerIdentity CreateCpuIdentity(int slot, int level) {
            if (!playerSelectSetting.TryGetStriker(slot, out var striker)) throw new InvalidOperationException($"Player {slot} has no selected striker.");
            return new PlayerIdentity {
                name = appStrikerRegistry.GetByStriker(striker).DisplayName, isCpu = true, cpuLevel = level,
                grade = level >= 1 && level <= 4 ? (int)PlayerGrades.Cpu(level, striker) : -1
            };
        }

        async Task<bool> PrepareTutorialAsync() {
            if (playerSelectSetting.TryGetStriker(0, out var striker)) {
                showingCharacterIntro = true;
                bool completed;
                try { completed = await view.CharacterIntroSlideshow.PlayAsync(striker); }
                finally { showingCharacterIntro = false; }
                if (disposed) return false;
                if (!completed) { inputState = SceneInputState.ReadyToStart; return false; }
            }
            foreach (var selection in tutorialSetting.BattleSelection.PlayerSelections) {
                if (selection.PlayerId != 0) playerSelectSetting.SelectStriker(selection.PlayerId, selection.Striker);
            }
            PrepareTutorialParticipants();
            return true;
        }

        void PrepareTutorialParticipants() {
            var name = playerSession.GetName(0);
            var player = string.IsNullOrWhiteSpace(name) ? new PlayerIdentity { name = "1P" } : profiles.Get(name).Identity;
            playerSession.SetMode(BattlePlayMode.Tutorial);
            playerSession.Prepare(new[] { player, CreateCpuIdentity(1, 0) });
        }

        AppScene ResolvePlayScene() {
            return battleSelectSetting.SelectedStage.CurrentValue == Stage.Street
                ? AppScene.Street
                : AppScene.Live;
        }

        void RequestStageSelectTransition() {
            if (isOnlineMatchmakingInProgress) {
                Debug.Log($"{LOG_PREFIX} RequestStageSelectTransition: cancel online matchmaking requested");
                duelOperations.CancelMatchmaking();
                return;
            }

            if (IsTransitioning()) {
                Debug.Log($"{LOG_PREFIX} RequestStageSelectTransition ignored because transitioning. inputState={inputState}");
                return;
            }

            var nextScene = tutorialSetting.IsTutorialBattleRequested || playerSession.IsTrial ? AppScene.Menu : AppScene.StageSelect;
            Debug.Log($"{LOG_PREFIX} RequestStageSelectTransition requesting start transition. nextScene={nextScene}");
            var result = transitionService.RequestStartTransition(nextScene);
            if (!result.IsSuccess) {
                Debug.LogWarning($"{LOG_PREFIX} RequestStageSelectTransition failed. nextScene={AppScene.StageSelect}");
                return;
            }

            if (tutorialSetting.IsTutorialBattleRequested) tutorialSetting.ClearTutorialBattleRequest();
            inputState = SceneInputState.TransitioningToStageSelect;
            Debug.Log($"{LOG_PREFIX} RequestStageSelectTransition accepted. inputState={inputState}");
        }

        bool TryUndoSelection() {
            if (selectionPolicy.TryPopUndoSlot(playerSelectSetting, out var slot)) {
                playerSelectSetting.DeselectStriker(slot);
            }
            else if (TryResolveUndoSlotFallback(out var fallbackSlot)) {
                Debug.LogWarning($"{LOG_PREFIX} UndoSelection fallback used. slot={fallbackSlot}");
                playerSelectSetting.DeselectStriker(fallbackSlot);
            }
            else {
                Debug.Log($"{LOG_PREFIX} UndoSelection skipped because no selected slot was found");
                return false;
            }

            RefreshState();
            return true;
        }

        async Task MatchOnlineAsync() {
            if (!playerSelectSetting.TryGetStriker(0, out var localStriker)) {
                throw new InvalidOperationException("Online battle requires player 0 striker selection.");
            }

            await duelOperations.NotifyPlayerStatusAsync(OnlineDuelPlayerStatus.Waiting);
            var matchingState = matchingModel.State.CurrentValue;
            var reservationId = matchingState.ReservationId;
            if (!matchingState.HasReservation || string.IsNullOrWhiteSpace(reservationId)) {
                throw new InvalidOperationException("Online battle requires a reservation.");
            }

            duelOperations.ConsumeReservation();
            var request = new OnlineMatchRequest(
                localStriker,
                battleSelectSetting.SelectedStage.CurrentValue,
                battleSelectSetting.SelectedMusicId.CurrentValue,
                reservationId,
                matchingState.LocalSessionId, profiles.Get(playerSession.GetName(0)).Identity);
            var result = await duelOperations.MatchAsync(request);
            if (result.LocalPlayer == null || result.OpponentPlayer == null || string.IsNullOrEmpty(result.BattleId)) throw new InvalidOperationException("対戦相手のプレイヤー情報を取得できません。リレーサーバーを更新してください。");
            playerSession.SetMode(BattlePlayMode.Online);
            playerSession.Prepare(result.LocalIsPlayer1 ? new[] { result.LocalPlayer, result.OpponentPlayer } : new[] { result.OpponentPlayer, result.LocalPlayer }, result.BattleId);
            Debug.Log($"{LOG_PREFIX} MatchAsync returned. Applying match result to settings before battle transition.");

            battleSelectSetting.SelectStage(result.Stage);
            battleSelectSetting.SelectMusic(result.MusicId);
            playerSelectSetting.ResetSelections();
            selectionPolicy.Reset(playerSelectSetting);
            var localPlayerId = result.LocalIsPlayer1 ? 0 : 1;
            var opponentPlayerId = result.LocalIsPlayer1 ? 1 : 0;
            appNetworkSetting.SetLocalOnlinePlayerId(localPlayerId);
            if (localPlayerId != 0) {
                gamePadRegistry.HandlePlayerSlotClick(0, localPlayerId);
            }
            gamePadRegistry.RequestRegister(opponentPlayerId, new RemoteGamePad(opponentPlayerId));
            playerSelectSetting.SelectStriker(localPlayerId, result.LocalStriker);
            playerSelectSetting.SelectStriker(opponentPlayerId, result.OpponentStriker);
            selectionPolicy.RecordSelection(0);
            selectionPolicy.RecordSelection(1);
            Debug.Log($"{LOG_PREFIX} Online match applied. stage={result.Stage}, musicId={result.MusicId}, local={result.LocalStriker}, opponent={result.OpponentStriker}, localPlayerId={localPlayerId}");
            Debug.Log($"{LOG_PREFIX} Online match settings applied. Proceeding to RequestStartTransition for battle scene.");
        }

        bool TryResolveUndoSlotFallback(out int slot) {
            var requiredSlots = GetRequiredSlotCount();
            for (var playerId = requiredSlots - 1; playerId >= 0; playerId--) {
                if (playerSelectSetting.TryGetStriker(playerId, out _)) {
                    slot = playerId;
                    return true;
                }
            }

            slot = -1;
            return false;
        }

        void RefreshState() {
            EnforceSelectionModeInvariant();
            RefreshReadyState();
            RefreshStatusView();
        }

        void RefreshReadyState() {
            var allSelected = CanStartBattle();
            view.StartButtonAnimation.SetAllStrikersSelected(allSelected);

            if (allSelected && !wasReadyToStart) {
                eastStartConfirmationArmed = true;
            }
            else if (!allSelected) {
                eastStartConfirmationArmed = false;
            }

            wasReadyToStart = allSelected;

            if (!IsTransitioning()) {
                inputState = allSelected ? SceneInputState.ReadyToStart : SceneInputState.Selecting;
            }
            PublishOnlineCharacterSelectStatus();
        }

        void PublishOnlineCharacterSelectStatus() {
            if (!IsOnlineSelectionFlow() || !onlineScenePresenceReady || IsTransitioning()) {
                hasPublishedOnlineStatus = false;
                return;
            }

            var status = OnlineDuelPlayerStatus.CharacterSelecting;
            if (hasPublishedOnlineStatus && lastPublishedOnlineStatus == status) {
                return;
            }

            hasPublishedOnlineStatus = true;
            lastPublishedOnlineStatus = status;
            _ = duelOperations.NotifyPlayerStatusAsync(status);
        }

        bool IsTransitioning() {
            return inputState == SceneInputState.Dialog || inputState == SceneInputState.TransitioningToScreen
                || inputState == SceneInputState.TransitioningToStageSelect;
        }

        bool CanStartBattle() {
            var requiredSlots = GetRequiredSlotCount();
            if (requiredSlots <= 0) {
                return false;
            }

            for (var playerId = 0; playerId < requiredSlots; playerId++) {
                if (!playerSelectSetting.TryGetStriker(playerId, out _)) {
                    return false;
                }
            }

            return true;
        }

        bool IsOnlineSelectionFlow() {
             return !playerSession.IsTrial && (matchingModel.IsEstablished.CurrentValue
                 || matchingModel.State.CurrentValue.HasReservation);
        }

        int GetRequiredSlotCount() {
            return playerSession.IsTrial || tutorialSetting.IsTutorialBattleRequested || IsOnlineSelectionFlow()
                ? 1
                : selectionPolicy.GetRequiredSlotCount(GetJoinedPlayerCount());
        }

        void EnforceSelectionModeInvariant() {
            if (!IsOnlineSelectionFlow() || IsTransitioning()) {
                return;
            }

            for (var playerId = 1; playerId < CharacterSelectSelectionPolicy.MAXPLAYERS; playerId++) {
                playerSelectSetting.DeselectStriker(playerId);
            }
        }

        int GetJoinedPlayerCount() {
            var joinedPlayers = 0;
            for (var playerId = 0; playerId < CharacterSelectSelectionPolicy.MAXPLAYERS; playerId++) {
                if (gamePadRegistry.Get(playerId).HasGamePad.CurrentValue) {
                    joinedPlayers++;
                }
            }

            return joinedPlayers;
        }

        void RefreshStatusView() {
            var requiredSlots = GetRequiredSlotCount();
            slotStates.Clear();

            for (var i = 0; i < requiredSlots; i++) {
                var hasGamePad = gamePadRegistry.Get(i).HasGamePad.CurrentValue;
                Sprite selectedPortrait = null;
                var isSelected = false;

                if (playerSelectSetting.TryGetStriker(i, out var striker)) {
                    isSelected = true;
                    selectedPortrait = appStrikerRegistry.GetByStriker(striker).Portrait;
                }

                var isWaiting = IsOnlineSelectionFlow() && i == 0 && isOnlineMatchmakingInProgress;
                slotStates.Add(new CharacterSelectSlotState(i, hasGamePad, isSelected, isWaiting, selectedPortrait));
            }

            view.StatusView.Render(slotStates);
        }

        public void Dispose() {
            disposed = true;
            cpuDialog.Dispose();
            introductionDialog.Dispose();
            if (isOnlineMatchmakingInProgress) {
                duelOperations.CancelMatchmaking();
            }
            subscriptions.Dispose();
        }
    }
}
