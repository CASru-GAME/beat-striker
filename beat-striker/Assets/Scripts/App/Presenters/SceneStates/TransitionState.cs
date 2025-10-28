

using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene {
    public class TransitionState : ISceneState {
        private readonly SceneStateContext context;
        private readonly AppScene nextScene;
        private readonly ISceneState nextState;

        public TransitionState(SceneStateContext context, AppScene nextScene) {
            this.context = context;
            this.nextScene = nextScene;
            this.nextState = context.factory.CreateSceneState(nextScene, context);
        }

        public void Enter() {
            context.bus.Publish(new AppMessages.OnTransitionAnimationStarted(nextScene));
            context.bus.Subscribe<AppMessages.RequireLoadScene>(OnAppFlowMessage);
        }

        private void OnAppFlowMessage(AppMessages.RequireLoadScene message) {
            context.view.LoadScene(nextScene, OnSceneLoadCompleted);
        }

        private void OnSceneLoadCompleted(AppScene scene) {
            if (scene == nextScene) {
                context.controller.ChangeState(nextState);
            }
        }

        public void Exit() {
            context.bus.Unsubscribe<AppMessages.RequireLoadScene>(OnAppFlowMessage);
        }
    }
}