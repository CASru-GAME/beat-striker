using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using UnityEngine;

namespace Alice {
    public class SelectPresenter : System.IDisposable {
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
        readonly CompositeDisposable subscriptions = new();
        readonly CharacterSelectSelectionPolicy selectionPolicy = new();
        readonly List<CharacterSelectSlotState> slotStates = new();
        readonly Dictionary<Striker, GameObject> strikerModelMap = new();
        readonly List<LoadedAsset<GameObject>> previewModelAssets = new();
        bool initialized;
        bool startTransitionInputEnabled;
        bool eastStartConfirmationArmed;
        bool wasReadyToStart;
        SceneInputState inputState = SceneInputState.Selecting;

        public SelectPresenter(
            SelectScene view,
            ISceneTransitionService transitionService,
            IGamePadRegistry gamePadRegistry,
            IBattleSelectSetting battleSelectSetting,
            IPlayerSelectSetting playerSelectSetting,
            IAppStrikerRegistry appStrikerRegistry) {
            this.view = view;
            this.transitionService = transitionService;
            this.gamePadRegistry = gamePadRegistry;
            this.battleSelectSetting = battleSelectSetting;
            this.playerSelectSetting = playerSelectSetting;
            this.appStrikerRegistry = appStrikerRegistry;

            _ = InitializeAfterFrameAsync();
        }

        async Awaitable InitializeAfterFrameAsync() {
            await Task.Yield();
            Debug.Log($"{LOG_PREFIX} InitializeAfterFrameAsync resumed and will initialize");
            Initialize();
        }

        void Initialize() {
            if (initialized) {
                Debug.Log($"{LOG_PREFIX} Initialize skipped because already initialized");
                return;
            }

            Debug.Log($"{LOG_PREFIX} Initialize start. buttonCount={view.CharacterSelectButtons.Length}");

            selectionPolicy.Reset(playerSelectSetting);
            BuildStrikerModelMap();

            for (var i = 0; i < view.CharacterSelectButtons.Length; i++) {
                view.CharacterSelectButtons[i].OnStrikerClicked
                    .Subscribe(OnStrikerClicked)
                    .AddTo(subscriptions);
            }

            view.Backbutton.OnBackPressed
                .Subscribe(_ => RequestStageSelectTransition())
                .AddTo(subscriptions);

            view.StartButtonAnimation.OnStartRequested
                .Subscribe(_ => RequestPlaySceneTransition(true))
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
            Debug.Log($"{LOG_PREFIX} EnableStartInputAfterSceneEnterAsync completed. isSuccess={result.IsSuccess}, startTransitionInputEnabled={startTransitionInputEnabled}");
        }

        void OnStrikerClicked(StrikerClickRequest request) {
            if (IsTransitioning()) {
                return;
            }

            var targetSlot = selectionPolicy.ResolveSelectionTargetSlot(
                request.PlayerId,
                GetJoinedPlayerCount(),
                playerSelectSetting.TryGetStriker(0, out _));

            playerSelectSetting.SelectStriker(targetSlot, request.Striker);
            selectionPolicy.RecordSelection(targetSlot);

            RefreshState();
        }

        void OnButtonDown(GamePadButton button) {
            if (IsTransitioning()) {
                return;
            }

            if (button == GamePadButton.South) {
                Debug.Log($"{LOG_PREFIX} OnButtonDown South received. undo selection requested");
                UndoSelection();
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

            RequestPlaySceneTransition(false);
        }

        void RequestPlaySceneTransition(bool requireStartButtonReady) {
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

            var nextScene = ResolvePlayScene();
            Debug.Log($"{LOG_PREFIX} RequestPlaySceneTransition requesting start transition. nextScene={nextScene}");
            var result = transitionService.RequestStartTransition(nextScene);
            if (!result.IsSuccess) {
                Debug.LogWarning($"{LOG_PREFIX} RequestPlaySceneTransition failed. nextScene={nextScene}");
                inputState = SceneInputState.ReadyToStart;
                return;
            }

            inputState = SceneInputState.TransitioningToScreen;
            Debug.Log($"{LOG_PREFIX} RequestPlaySceneTransition accepted. inputState={inputState}, nextScene={nextScene}");
            view.ClickSound.PlayAtApp(Camera.main.transform.position);
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

        bool TryResolveUndoSlotFallback(out int slot) {
            var requiredSlots = selectionPolicy.GetRequiredSlotCount(GetJoinedPlayerCount());
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
        }

        bool IsTransitioning() {
            return inputState == SceneInputState.TransitioningToScreen
                || inputState == SceneInputState.TransitioningToStageSelect;
        }

        bool CanStartBattle() {
            var requiredSlots = selectionPolicy.GetRequiredSlotCount(GetJoinedPlayerCount());
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

        int GetJoinedPlayerCount() {
            var joinedPlayers = 0;
            for (var playerId = 0; playerId < CharacterSelectSelectionPolicy.MAXPLAYERS; playerId++) {
                if (gamePadRegistry.Get(playerId).HasGamePad.CurrentValue) {
                    joinedPlayers++;
                }
            }

            return joinedPlayers;
        }

        void BuildStrikerModelMap() {
            strikerModelMap.Clear();
            var strikers = appStrikerRegistry.GetAll();
            for (var i = 0; i < strikers.Count; i++) {
                var info = strikers[i];
                var modelAsset = appStrikerRegistry.LoadPreviewModel(info.BattleStriker);
                previewModelAssets.Add(modelAsset);
                var modelPrefab = modelAsset.Asset;
                if (modelPrefab != null) {
                    strikerModelMap[info.BattleStriker] = modelPrefab;
                }
            }
        }

        void RefreshStatusView() {
            var requiredSlots = selectionPolicy.GetRequiredSlotCount(GetJoinedPlayerCount());
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
            subscriptions.Dispose();
            for (var i = 0; i < previewModelAssets.Count; i++) {
                previewModelAssets[i].Dispose();
            }
            previewModelAssets.Clear();
        }
    }
}
