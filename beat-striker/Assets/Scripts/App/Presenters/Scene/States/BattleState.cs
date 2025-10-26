
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using UnityEditor.SceneManagement;

namespace Core.App.Presenters.Scene.States {

    public class BattleState : ISceneState {
        private readonly SceneStateContext context;

        public BattleState(SceneStateContext context) {
            this.context = context;
            context.bus.Subscribe<TransitionMessage>(OnAppFlowMessage);
        }

        private void OnAppFlowMessage(TransitionMessage message) {
            if (message.command == TransitionCommand.Next) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Battle
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