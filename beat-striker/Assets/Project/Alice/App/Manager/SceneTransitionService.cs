using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alice {

    public interface ISceneTransitionService {
        StartResult RequestStartTransition(AppScene nextScene);
        Task<ExitResult> RequestEndTransitionAsync(AppScene currentScene);

        public record StartResult(bool IsSuccess);
        public record ExitResult(bool IsSuccess);
    }

    public class SceneTransitionService : ISceneTransitionService {

        readonly ISceneLoader sceneLoader;
        readonly IAppTransitionFactory transitionFactory;
        readonly IScreenRegistry screenRegistry;
        readonly IAppBGMPlayer appBgmPlayer;

        TransitionState currentState = TransitionState.Idle;
        AppScene currentScene;
        IAppTransitionPresenter currentTransition;

        enum TransitionState {
            Ready,
            Entering,
            Idle,
            Exiting,
            Loading,
        }

        public SceneTransitionService(
            ISceneLoader sceneLoader,
            IAppTransitionFactory transitionFactory,
            IScreenRegistry screenRegistry,
            IAppBGMPlayer appBgmPlayer) {
            this.sceneLoader = sceneLoader;
            this.transitionFactory = transitionFactory;
            this.screenRegistry = screenRegistry;
            this.appBgmPlayer = appBgmPlayer;

            var currentScreen = screenRegistry.GetBySceneName(SceneManager.GetActiveScene().name);
            currentScene = currentScreen.Scene;
            appBgmPlayer.Play(currentScreen.Bgm);
        }

        public ISceneTransitionService.StartResult RequestStartTransition(AppScene nextScene) {
            if (currentState != TransitionState.Idle) {
                Debug.LogWarning($"Cannot start transition to {nextScene} because current state is {currentState}");
                return new ISceneTransitionService.StartResult(false);
            }

            var request = new AppTransitionRequest(currentScene, nextScene);
            currentTransition = transitionFactory.Create(request);

            currentState = TransitionState.Exiting;
            _ = RunStartTransitionAsync(nextScene);

            return new ISceneTransitionService.StartResult(true);
        }

        async Task RunStartTransitionAsync(AppScene nextScene) {
            try {
                await currentTransition.PresentTransitionOut(new TransitionContext());


                currentState = TransitionState.Loading;
                await sceneLoader.LoadAsync(nextScene);
                Debug.Log($"Scene {nextScene} loaded".ToCyan());

                currentScene = nextScene;
                appBgmPlayer.Play(screenRegistry.GetByScene(nextScene).Bgm);
                
                currentState = TransitionState.Ready;
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                currentState = TransitionState.Idle;
            }
        }

        public async Task<ISceneTransitionService.ExitResult> RequestEndTransitionAsync(AppScene scene) {
            await WaitForReadyOrIdleAsync();

            if (currentState == TransitionState.Idle) {
                Debug.LogWarning($"Cannot end transition for {scene} because current state is Idle");
                return new ISceneTransitionService.ExitResult(true);
            }

            if (currentState != TransitionState.Ready) {
                Debug.LogWarning($"Cannot end transition for {scene} because current state is {currentState}");
                return new ISceneTransitionService.ExitResult(false);
            }

            if (currentTransition == null) {
                Debug.LogWarning($"No transition found for scene {scene}. Skipping transition.");
                currentState = TransitionState.Idle;
                return new ISceneTransitionService.ExitResult(true);
            }
            
            try {
                currentState = TransitionState.Entering;
                await currentTransition.PresentTransitionIn(new TransitionContext());

                currentTransition.DestroyGameObject();
                currentTransition = null;

                currentState = TransitionState.Idle;

                return new ISceneTransitionService.ExitResult(true);
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                currentState = TransitionState.Idle;
                return new ISceneTransitionService.ExitResult(false);
            }
        }

        async Task WaitForReadyOrIdleAsync() {
            while (currentState == TransitionState.Exiting || currentState == TransitionState.Loading) {
                await Task.Yield();
            }
        }
    }
}
