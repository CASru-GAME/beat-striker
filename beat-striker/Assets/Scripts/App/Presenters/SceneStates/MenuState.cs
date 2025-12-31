using System;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;

namespace Core.App.Presenters.Scene.States {

    public class MenuState : ISceneState {
        private readonly SceneStateContext context;
        private IDisposable transitionSubscription;

        public MenuState(SceneStateContext context) {
            this.context = context;
        }

        private void OnRequireTransition(AppScene scene) {
            if (scene == AppScene.Title || scene == AppScene.Menu || scene == AppScene.StageSelect || scene == AppScene.CharacterSelect) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    scene
                ));
            }
        }

        public void Enter() {
            context.cursorRegistry.SetCursorsActive(true);
            transitionSubscription = context.events.SubscribeRequireTransition(OnRequireTransition);
            context.events.FirePlayBGM(BGMType.MainBGM);
        }

        public void Exit() {
            transitionSubscription?.Dispose();
        }
    }
}
