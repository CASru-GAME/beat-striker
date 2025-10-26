

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

        public async void Enter() {
            context.view.StartTransitionAnimation();
            context.bus.Subscribe<TransitionMessage>(OnAppFlowMessage);
            await context.view.LoadSceneAsync(nextScene);
        }

        private void OnAppFlowMessage(TransitionMessage message) {
            if (message.command == TransitionCommand.End) {
                context.controller.ChangeState(context.factory.CreateSceneState(nextScene, context));
            }
        }

        public void Exit() {
            context.bus.Unsubscribe<TransitionMessage>(OnAppFlowMessage);
        }
    }
}