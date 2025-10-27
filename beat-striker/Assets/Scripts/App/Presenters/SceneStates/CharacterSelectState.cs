
using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene.States {

    public class CharacterSelectState : ISceneState {
        private readonly SceneStateContext context;

        public CharacterSelectState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(AppMessages.RequireTransition message) {
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

        private void OnStrikerSelected(AppMessages.SelectStriker message) {
            context.setting.SetStriker(message.playerId, message.striker);
        }

        public void Enter() {
            context.cursorRegistry.SetCursorsActive(true);
            context.bus.Subscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.bus.Subscribe<AppMessages.SelectStriker>(OnStrikerSelected);
        }

        public void Exit() {
            context.bus.Unsubscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.bus.Unsubscribe<AppMessages.SelectStriker>(OnStrikerSelected);
        }
    }
}