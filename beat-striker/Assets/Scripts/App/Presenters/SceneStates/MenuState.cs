using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene.States {

    public class MenuState : ISceneState {
        private readonly SceneStateContext context;

        public MenuState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(AppMessages.RequireTransition message) {
            if (message.scene == AppScene.Title || message.scene == AppScene.Menu || message.scene == AppScene.StageSelect || message.scene == AppScene.CharacterSelect) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    message.scene
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
