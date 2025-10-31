

using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using UnityEngine;

namespace Core.App.Presenters.Scene {
    public class TransitionState : ISceneState {
        private readonly SceneStateContext context;
        private readonly AppScene nextScene;
        private readonly ISceneState nextState;
        private bool sceneLoadRequested = false;

        public TransitionState(SceneStateContext context, AppScene nextScene) {
            this.context = context;
            this.nextScene = nextScene;
            this.nextState = context.factory.CreateSceneState(nextScene, context);
        }

        public void Enter() {
            sceneLoadRequested = false;
            context.bus.Publish(new AppMessages.OnTransitionAnimationStarted(nextScene));
            context.bus.Subscribe<AppMessages.RequireLoadScene>(OnAppFlowMessage);
            CheckTimeout();
        }

        private async void CheckTimeout() {
            await System.Threading.Tasks.Task.Delay(5000);
            if (!sceneLoadRequested) {
                OnSceneLoadCompleted(nextScene);
            }
        }

        private void OnAppFlowMessage(AppMessages.RequireLoadScene message) {
            sceneLoadRequested = true;
            context.view.LoadScene(nextScene, OnSceneLoadCompleted);
        }

        private void OnSceneLoadCompleted(AppScene scene) {
            if (scene == nextScene) {
                context.controller.ChangeState(nextState);
            }
        }

        public void Exit() {
            context.bus.Unsubscribe<AppMessages.RequireLoadScene>(OnAppFlowMessage);
        }
    }
}