using System;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;

namespace Core.App.Presenters.Scene.States {

    public class TitleState : ISceneState {
        private readonly SceneStateContext context;
        private IDisposable transitionSubscription;

        public TitleState(SceneStateContext context) {
            this.context = context;
        }

        private void OnRequireTransition(AppScene scene) {
            if (scene == AppScene.Menu) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Menu
                ));
            }
            else if (scene == AppScene.StageSelect) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.StageSelect
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