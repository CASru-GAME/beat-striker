using System;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;

namespace Core.App.Presenters.Scene.States {

    public class BattleState : ISceneState {
        private readonly SceneStateContext context;
        private IDisposable transitionSubscription;

        public BattleState(SceneStateContext context) {
            this.context = context;
        }

        private void OnRequireTransition(AppScene scene) {
            if (scene == AppScene.Menu) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    scene
                ));
            }
        }

        public void Enter() {
            transitionSubscription = context.events.SubscribeRequireTransition(OnRequireTransition);
            context.cursorRegistry.SetCursorsActive(false);
            context.events.FireStopBGM(); 
        }

        public void Exit() {
            transitionSubscription?.Dispose();
        }
    }
}

