using System;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.App.Presenters.Scene {
    public class TransitionState : ISceneState {
        private readonly SceneStateContext context;
        private readonly AppScene nextScene;
        private readonly ISceneState nextState;
        private bool sceneLoadRequested = false;
        private IDisposable loadSceneSubscription;

        public TransitionState(SceneStateContext context, AppScene nextScene) {
            this.context = context;
            this.nextScene = nextScene;
            this.nextState = context.factory.CreateSceneState(nextScene, context);
        }

        public void Enter() {
            sceneLoadRequested = false;
            context.events.FireTransitionAnimationStarted(nextScene);
            loadSceneSubscription = context.events.SubscribeRequireLoadScene(OnRequireLoadScene);
            CheckTimeout();
        }

        private async void CheckTimeout() {
            await System.Threading.Tasks.Task.Delay(5000);
            if (!sceneLoadRequested) {
                OnSceneLoadCompleted(nextScene);
            }
        }

        private void OnRequireLoadScene() {
            sceneLoadRequested = true;
            context.view.LoadScene(nextScene, OnSceneLoadCompleted);
        }

        private void OnSceneLoadCompleted(AppScene scene) {
            if (scene == nextScene) {
                context.controller.ChangeState(nextState);
            }
        }

        public void Exit() {
            loadSceneSubscription?.Dispose();
        }
    }
}