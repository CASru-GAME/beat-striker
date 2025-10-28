
using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene.States {

    public class BattleState : ISceneState {
        private readonly SceneStateContext context;

        public BattleState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(AppMessages.RequireTransition message) {
            if (message.scene == AppScene.Title) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Title
                ));
            }
        }

        public void Enter() {
            context.bus.Subscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.cursorRegistry.SetCursorsActive(false);
        }

        public void Exit() {
            context.bus.Unsubscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
        }
    }
}

