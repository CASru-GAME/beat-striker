
using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene.States {

    public class CharacterSelectState : ISceneState {
        private readonly SceneStateContext context;

        public CharacterSelectState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(RequireTransitionMessage message) {
            if (message.command == TransitionRequire.StartExitAnimation) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.StageSelect
                ));
            }
            else if (message.command == TransitionRequire.Next) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Battle
                ));
            }
        }

        private void OnStrikerSelected(SelectStrikerMessage message) {
            context.setting.SetStriker(message.playerId, message.striker);
        }

        public void Enter() {
            context.bus.Subscribe<RequireTransitionMessage>(OnAppFlowMessage);
            context.bus.Subscribe<SelectStrikerMessage>(OnStrikerSelected);
        }

        public void Exit() {
            context.bus.Unsubscribe<RequireTransitionMessage>(OnAppFlowMessage);
            context.bus.Unsubscribe<SelectStrikerMessage>(OnStrikerSelected);
        }
    }
}