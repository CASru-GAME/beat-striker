

using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene.States {

    public class TitleState : ISceneState {
        private readonly SceneStateContext context;

        public TitleState(SceneStateContext context) {
            this.context = context;
            context.bus.Subscribe<TransitionMessage>(OnAppFlowMessage);
        }

        private void OnAppFlowMessage(TransitionMessage message) {
            if (message.command == TransitionCommand.Next) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.StageSelect
                ));
            }
        }

        public async void Enter() {
            context.bus.Unsubscribe<TransitionMessage>(OnAppFlowMessage);
        }

        public void Exit() {
        }
    }
}