

using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene {
    public class TransitionState : ISceneState {
        private readonly SceneStateContext context;
        private readonly AppScene nextScene;

        public TransitionState(SceneStateContext context, AppScene nextScene) {
            this.context = context;
            this.nextScene = nextScene;
        }

        public void Enter() {
            context.bus.Publish(new TransitionStartedMessage(nextScene));
            context.bus.Subscribe<RequireTransitionMessage>(OnAppFlowMessage);


        }
        
        private void OnAppFlowMessage(RequireTransitionMessage message) {
            if(message.command == TransitionRequire.LoadScene){
                context.view.LoadScene(nextScene, OnSceneLoadCompleted);
            }
        }

        private void OnSceneLoadCompleted(AppScene scene) {
            if (scene == nextScene) {
                context.controller.ChangeState(context.factory.CreateSceneState(nextScene, context));
            }
        }

        public void Exit() {
            context.bus.Unsubscribe<RequireTransitionMessage>(OnAppFlowMessage);
        }
    }
}