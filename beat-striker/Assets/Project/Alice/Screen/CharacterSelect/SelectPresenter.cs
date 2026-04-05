using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using UnityEngine;

namespace Alice {
    public class SelectPresenter : System.IDisposable {
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
        bool initialized;
        bool startTransitionInputEnabled;
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
            Initialize();
        }

        void Initialize() {
            if (initialized) {
                return;
            }

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
                .Subscribe(_ => RequestPlaySceneTransition())
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
        }

        async Awaitable EnableStartInputAfterSceneEnterAsync() {
            startTransitionInputEnabled = false;
            await transitionService.RequestEndTransitionAsync(AppScene.CharacterSelect);
            startTransitionInputEnabled = true;
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
                UndoSelection();
                return;
            }

            if (button != GamePadButton.East) return;
            RequestPlaySceneTransition();
        }

        void RequestPlaySceneTransition() {
            if (IsTransitioning()) {
                return;
            }

            if (inputState != SceneInputState.ReadyToStart) {
                return;
            }

            if (!startTransitionInputEnabled) {
                return;
            }

            if (!view.StartButtonAnimation.IsStartInputReady) {
                return;
            }

            var result = transitionService.RequestStartTransition(ResolvePlayScene());
            if (!result.IsSuccess) {
                inputState = SceneInputState.ReadyToStart;
                return;
            }

            inputState = SceneInputState.TransitioningToScreen;
            AudioSource.PlayClipAtPoint(view.ClickSound, Camera.main.transform.position);
        }

        AppScene ResolvePlayScene() {
            return battleSelectSetting.SelectedStage.CurrentValue == Stage.Street
                ? AppScene.Street
                : AppScene.Live;
        }

        void RequestStageSelectTransition() {
            if (IsTransitioning()) {
                return;
            }

            var result = transitionService.RequestStartTransition(AppScene.StageSelect);
            if (!result.IsSuccess) {
                return;
            }

            inputState = SceneInputState.TransitioningToStageSelect;
        }

        void UndoSelection() {
            if (selectionPolicy.TryPopUndoSlot(playerSelectSetting, out var slot)) {
                playerSelectSetting.DeselectStriker(slot);
            }

            RefreshState();
        }

        void RefreshState() {
            RefreshReadyState();
            RefreshStatusView();
        }

        void RefreshReadyState() {
            var allSelected = CanStartBattle();
            view.StartButtonAnimation.SetAllStrikersSelected(allSelected);

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
                strikerModelMap[info.BattleStriker] = info.PreviewModel;
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
        }
    }
}
