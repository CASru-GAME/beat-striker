using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene.States {

    public class TitleState : ISceneState {
        private readonly SceneStateContext context;

        public TitleState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(RequireTransitionMessage message) {
            if (message.command == TransitionRequire.Next) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.StageSelect
                ));
            }
        }

        public void Enter() {
            context.bus.Subscribe<RequireTransitionMessage>(OnAppFlowMessage);
        }

        public void Exit() {
            context.bus.Unsubscribe<RequireTransitionMessage>(OnAppFlowMessage);
        }
    }
}