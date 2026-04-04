using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene.States {

    public class TitleState : ISceneState {
        private readonly SceneStateContext context;

        public TitleState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(AppMessages.RequireTransition message) {
            if (message.scene == FAFA.Menu) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    FAFA.Menu
                ));
            }
            else if (message.scene == FAFA.StageSelect) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    FAFA.StageSelect
                ));
            }
        }

        public void Enter() {
            context.cursorRegistry.SetCursorsActive(true);
            context.bus.Subscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.bus.Publish(new AppMessages.PlayBGM(BGMType.MainBGM));
        }

        public void Exit() {
            context.bus.Unsubscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
        }
    }
}