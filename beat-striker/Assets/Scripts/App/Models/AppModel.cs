
using System;
using Core.App.Interfaces;
using Core.App.Presenters.Scene;
using Core.App.Presenters.Scene.States;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;

namespace Core.App.Models {
    public class AppModel : IAppModel, ISceneStateFactory {
        
        // State Machine
        private ISceneState currentState;
        
        // Scene Factory Context (dependency injection for states)
        private SceneStateContext context;

        // Observable Events (Logic from AppEvents)
        private readonly Subject<AppScene> onRequireTransition = new();
        private readonly Subject onRequireLoadScene = new();
        private readonly Subject<AppScene> onTransitionAnimationStarted = new();
        
        private readonly Subject<PlayerId> onPlayerJoined = new();
        private readonly Subject<PlayerId> onPlayerLeft = new();
        
        private readonly Subject<bool> onSetCursorsActive = new();
        private readonly Subject<int> onSetCursorSortingOrder = new();
        private readonly Subject<CursorDestroyRequest> onRequireCursorDestroyed = new();
        private readonly Subject<CursorPositionUpdate> onCursorPositionUpdated = new();
        
        private readonly Subject<StrikerSelection> onSelectStriker = new();
        private readonly Subject<StageId> onSelectStage = new();
        private readonly Subject<TrackId> onSelectTrack = new();
        private readonly Subject<bool> onAllStrikersSelectedChanged = new();
        
        private readonly Subject<BGMType> onPlayBGM = new();
        private readonly Subject onStopBGM = new();

        public void Initialize(AppScene firstScene, SceneStateContext context) {
            this.context = context;
            currentState = CreateSceneState(firstScene, context);
            currentState.Enter();
        }
        
        public void OnDisable() {
             currentState?.Exit();
        }

        public void ChangeState(ISceneState newState) {
            currentState?.Exit();
            currentState = newState;
            currentState?.Enter();
        }

        public ISceneState CreateSceneState(AppScene scene, SceneStateContext context) {
            return scene switch {
                AppScene.Title => new TitleState(context),
                AppScene.Menu => new MenuState(context),
                AppScene.StageSelect => new StageSelectState(context),
                AppScene.CharacterSelect => new CharacterSelectState(context),
                AppScene.Battle => new BattleState(context),
                AppScene.Battle_Stage => new BattleState(context),
                AppScene.Battle_Street => new BattleState(context),
                _ => new TitleState(context),
            };
        }

        // --- Subscriptions ---
        public IDisposable SubscribeRequireTransition(Action<AppScene> listener) => onRequireTransition.Subscribe(listener);
        public IDisposable SubscribeRequireLoadScene(Action listener) => onRequireLoadScene.Subscribe(listener);
        public IDisposable SubscribeTransitionAnimationStarted(Action<AppScene> listener) => onTransitionAnimationStarted.Subscribe(listener);
        
        public IDisposable SubscribePlayerJoined(Action<PlayerId> listener) => onPlayerJoined.Subscribe(listener);
        public IDisposable SubscribePlayerLeft(Action<PlayerId> listener) => onPlayerLeft.Subscribe(listener);
        
        public IDisposable SubscribeSetCursorsActive(Action<bool> listener) => onSetCursorsActive.Subscribe(listener);
        public IDisposable SubscribeSetCursorSortingOrder(Action<int> listener) => onSetCursorSortingOrder.Subscribe(listener);
        public IDisposable SubscribeRequireCursorDestroyed(Action<CursorDestroyRequest> listener) => onRequireCursorDestroyed.Subscribe(listener);
        public IDisposable SubscribeCursorPositionUpdated(Action<CursorPositionUpdate> listener) => onCursorPositionUpdated.Subscribe(listener);
        
        public IDisposable SubscribeSelectStriker(Action<StrikerSelection> listener) => onSelectStriker.Subscribe(listener);
        public IDisposable SubscribeSelectStage(Action<StageId> listener) => onSelectStage.Subscribe(listener);
        public IDisposable SubscribeSelectTrack(Action<TrackId> listener) => onSelectTrack.Subscribe(listener);
        public IDisposable SubscribeAllStrikersSelectedChanged(Action<bool> listener) => onAllStrikersSelectedChanged.Subscribe(listener);
        
        public IDisposable SubscribePlayBGM(Action<BGMType> listener) => onPlayBGM.Subscribe(listener);
        public IDisposable SubscribeStopBGM(Action listener) => onStopBGM.Subscribe(listener);
        
        // --- Fire Events ---
        public void FireRequireTransition(AppScene scene) => onRequireTransition.Fire(scene);
        public void FireRequireLoadScene() => onRequireLoadScene.Fire();
        public void FireTransitionAnimationStarted(AppScene scene) => onTransitionAnimationStarted.Fire(scene);
        
        public void FirePlayerJoined(PlayerId playerId) => onPlayerJoined.Fire(playerId);
        public void FirePlayerLeft(PlayerId playerId) => onPlayerLeft.Fire(playerId);
        
        public void FireSetCursorsActive(bool active) => onSetCursorsActive.Fire(active);
        public void FireSetCursorSortingOrder(int order) => onSetCursorSortingOrder.Fire(order);
        public void FireRequireCursorDestroyed(CursorDestroyRequest request) => onRequireCursorDestroyed.Fire(request);
        public void FireCursorPositionUpdated(CursorPositionUpdate update) => onCursorPositionUpdated.Fire(update);
        
        public void FireSelectStriker(StrikerSelection selection) => onSelectStriker.Fire(selection);
        public void FireSelectStage(StageId stageId) => onSelectStage.Fire(stageId);
        public void FireSelectTrack(TrackId trackId) => onSelectTrack.Fire(trackId);
        public void FireAllStrikersSelectedChanged(bool allSelected) => onAllStrikersSelectedChanged.Fire(allSelected);
        
        public void FirePlayBGM(BGMType bgmType) => onPlayBGM.Fire(bgmType);
        public void FireStopBGM() => onStopBGM.Fire();
    }
}
