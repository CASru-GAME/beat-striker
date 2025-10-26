
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using UnityEditor.SceneManagement;

namespace Core.App.Presenters.Scene.States {

    public class CharacterSelectState : ISceneState {
        private readonly SceneStateContext context;

        public CharacterSelectState(SceneStateContext context) {
            this.context = context;
            context.bus.Subscribe<TransitionMessage>(OnAppFlowMessage);
        }

        private void OnAppFlowMessage(TransitionMessage message) {
            if (message.command == TransitionCommand.Back) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.StageSelect
                ));
            }
            else if (message.command == TransitionCommand.Next) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.CharacterSelect
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