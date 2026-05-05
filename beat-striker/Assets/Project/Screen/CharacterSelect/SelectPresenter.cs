using System;
using System.Collections.Generic;
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
            TransitioningToScreen,
            TransitioningToStageSelect,
        }

        readonly SelectScene view;
        readonly ISceneTransitionService transitionService;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IOnlineSessionBootstrap onlineSessionBootstrap;
        readonly IOnlineDuelCoordinator onlineDuelCoordinator;
        readonly IOnlineDuelIdentity duelIdentity;
        readonly IOnlineDuelFusionClient onlineDuelFusionClient;
        readonly CompositeDisposable subscriptions = new();
        readonly CharacterSelectSelectionPolicy selectionPolicy = new();
        readonly List<CharacterSelectSlotState> slotStates = new();
        readonly Dictionary<Striker, GameObject> strikerModelMap = new();
        readonly List<LoadedAsset<GameObject>> previewModelAssets = new();
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
            IOnlineSessionBootstrap onlineSessionBootstrap,
            IOnlineDuelCoordinator onlineDuelCoordinator,
            IOnlineDuelIdentity duelIdentity,
            IOnlineDuelFusionClient onlineDuelFusionClient) {
            this.view = view;
            this.transitionService = transitionService;
            this.gamePadRegistry = gamePadRegistry;
            this.battleSelectSetting = battleSelectSetting;
            this.playerSelectSetting = playerSelectSetting;
            this.appStrikerRegistry = appStrikerRegistry;
            this.appNetworkSetting = appNetworkSetting;
            this.onlineSessionBootstrap = onlineSessionBootstrap;
            this.onlineDuelCoordinator = onlineDuelCoordinator;
            this.duelIdentity = duelIdentity;
            this.onlineDuelFusionClient = onlineDuelFusionClient;

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
            await BuildStrikerModelMapAsync();
            if (disposed) {
                return;
            }

            for (var i = 0; i < view.CharacterSelectButtons.Length; i++) {
                view.CharacterSelectButtons[i].OnStrikerClicked
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

                gamePadRegistry.Get(playerId).OnButtonDown
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
            startTransitionInputEnabled = true;
            onlineScenePresenceReady = true;
            PublishOnlineCharacterSelectStatus(CanStartBattle());
            Debug.Log($"{LOG_PREFIX} EnableStartInputAfterSceneEnterAsync completed. isSuccess={result.IsSuccess}, startTransitionInputEnabled={startTransitionInputEnabled}");
        }

        void OnStrikerClicked(StrikerClickRequest request) {
            if (IsTransitioning()) {
                return;
            }

            var targetSlot = IsOnlineSelectionFlow()
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
            if (button == GamePadButton.South) {
                if (isOnlineMatchmakingInProgress) {
                    Debug.Log($"{LOG_PREFIX} OnButtonDown South received. cancel online matchmaking requested");
                    onlineSessionBootstrap.CancelMatchmaking();
                    return;
                }

                if (IsTransitioning()) {
                    return;
                }

                Debug.Log($"{LOG_PREFIX} OnButtonDown South received. undo selection requested");
                UndoSelection();
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

            inputState = SceneInputState.TransitioningToScreen;

            if (IsOnlineSelectionFlow()) {
                EnsureOnlineModeForDuelFlow();
                view.StartButtonAnimation.SetOnlineWaitingPopupVisible(true);
                isOnlineMatchmakingInProgress = true;
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

        AppScene ResolvePlayScene() {
            return battleSelectSetting.SelectedStage.CurrentValue == Stage.Street
                ? AppScene.Street
                : AppScene.Live;
        }

        void RequestStageSelectTransition() {
            if (IsTransitioning()) {
                Debug.Log($"{LOG_PREFIX} RequestStageSelectTransition ignored because transitioning. inputState={inputState}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} RequestStageSelectTransition requesting start transition. nextScene={AppScene.StageSelect}");
            var result = transitionService.RequestStartTransition(AppScene.StageSelect);
            if (!result.IsSuccess) {
                Debug.LogWarning($"{LOG_PREFIX} RequestStageSelectTransition failed. nextScene={AppScene.StageSelect}");
                return;
            }

            inputState = SceneInputState.TransitioningToStageSelect;
            Debug.Log($"{LOG_PREFIX} RequestStageSelectTransition accepted. inputState={inputState}");
        }

        void UndoSelection() {
            if (selectionPolicy.TryPopUndoSlot(playerSelectSetting, out var slot)) {
                playerSelectSetting.DeselectStriker(slot);
            }
            else if (TryResolveUndoSlotFallback(out var fallbackSlot)) {
                Debug.LogWarning($"{LOG_PREFIX} UndoSelection fallback used. slot={fallbackSlot}");
                playerSelectSetting.DeselectStriker(fallbackSlot);
            }
            else {
                Debug.Log($"{LOG_PREFIX} UndoSelection skipped because no selected slot was found");
            }

            RefreshState();
        }

        async Task MatchOnlineAsync() {
            if (!playerSelectSetting.TryGetStriker(0, out var localStriker)) {
                throw new InvalidOperationException("Online battle requires player 0 striker selection.");
            }

            await onlineDuelCoordinator.NotifyPlayerStatusAsync(OnlineDuelPlayerStatus.Waiting);
            var reservationId = onlineDuelFusionClient.ReservationId;
            if (onlineDuelFusionClient.HasReservation) {
                onlineDuelFusionClient.ConsumeReservation();
            }

            var request = onlineDuelFusionClient.HasReservation
                ? new OnlineMatchRequest(
                    localStriker,
                    battleSelectSetting.SelectedStage.CurrentValue,
                    battleSelectSetting.SelectedMusicId.CurrentValue,
                    reservationId,
                    duelIdentity.DuelSessionId)
                : new OnlineMatchRequest(
                    localStriker,
                    battleSelectSetting.SelectedStage.CurrentValue,
                    battleSelectSetting.SelectedMusicId.CurrentValue);
            var result = await onlineSessionBootstrap.MatchAsync(request);
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
            EnsureOnlineModeForDuelFlow();
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
            PublishOnlineCharacterSelectStatus(allSelected);
        }

        void PublishOnlineCharacterSelectStatus(bool allSelected) {
            if (!IsOnlineSelectionFlow() || !onlineScenePresenceReady || IsTransitioning()) {
                hasPublishedOnlineStatus = false;
                return;
            }

            var status = allSelected
                ? OnlineDuelPlayerStatus.Waiting
                : OnlineDuelPlayerStatus.CharacterSelecting;
            if (hasPublishedOnlineStatus && lastPublishedOnlineStatus == status) {
                return;
            }

            hasPublishedOnlineStatus = true;
            lastPublishedOnlineStatus = status;
            _ = onlineDuelCoordinator.NotifyPlayerStatusAsync(status);
        }

        bool IsTransitioning() {
            return inputState == SceneInputState.TransitioningToScreen
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
            return appNetworkSetting.IsOnline.CurrentValue
                   || IsActiveDuelSelectionState(onlineDuelFusionClient.State.CurrentValue);
        }

        int GetRequiredSlotCount() {
            return IsOnlineSelectionFlow()
                ? 1
                : selectionPolicy.GetRequiredSlotCount(GetJoinedPlayerCount());
        }

        void EnsureOnlineModeForDuelFlow() {
            if (!appNetworkSetting.IsOnline.CurrentValue
                && IsActiveDuelSelectionState(onlineDuelFusionClient.State.CurrentValue)) {
                appNetworkSetting.SetIsOnline(true);
            }
        }

        void EnforceSelectionModeInvariant() {
            if (!IsOnlineSelectionFlow() || IsTransitioning()) {
                return;
            }

            for (var playerId = 1; playerId < CharacterSelectSelectionPolicy.MAXPLAYERS; playerId++) {
                playerSelectSetting.DeselectStriker(playerId);
            }
        }

        static bool IsActiveDuelSelectionState(OnlineDuelUiState state) {
            return state.HasReservation
                   || state.Phase == OnlineDuelPhase.Reserved
                   || state.Phase == OnlineDuelPhase.Consumed
                   || state.Phase == OnlineDuelPhase.Matching
                   || state.Phase == OnlineDuelPhase.EnterBattle;
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

        async Awaitable BuildStrikerModelMapAsync() {
            strikerModelMap.Clear();
            var strikers = appStrikerRegistry.GetAll();
            for (var i = 0; i < strikers.Count; i++) {
                var info = strikers[i];
                var modelAsset = await appStrikerRegistry.LoadPreviewModelAsync(info.BattleStriker);
                if (disposed) {
                    modelAsset.Dispose();
                    return;
                }

                previewModelAssets.Add(modelAsset);
                var modelPrefab = modelAsset.Asset;
                if (modelPrefab != null) {
                    strikerModelMap[info.BattleStriker] = modelPrefab;
                }
            }
        }

        void RefreshStatusView() {
            var requiredSlots = GetRequiredSlotCount();
            slotStates.Clear();

            for (var i = 0; i < requiredSlots; i++) {
                var hasGamePad = gamePadRegistry.Get(i).HasGamePad.CurrentValue;
                GameObject selectedModelPrefab = null;
                var isSelected = false;

                if (playerSelectSetting.TryGetStriker(i, out var striker) && strikerModelMap.TryGetValue(striker, out var modelPrefab)) {
                    isSelected = true;
                    selectedModelPrefab = modelPrefab;
                }
                else if (playerSelectSetting.TryGetStriker(i, out _)) {
                    isSelected = true;
                }

                slotStates.Add(new CharacterSelectSlotState(i, hasGamePad, isSelected, selectedModelPrefab));
            }

            view.StatusView.Render(slotStates);
        }

        public void Dispose() {
            disposed = true;
            subscriptions.Dispose();
            for (var i = 0; i < previewModelAssets.Count; i++) {
                previewModelAssets[i].Dispose();
            }
            previewModelAssets.Clear();
        }
    }
}
