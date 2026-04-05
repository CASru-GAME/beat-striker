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
        const string LOG_PREFIX = "[SceneTransitionService]";

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
            Debug.Log($"{LOG_PREFIX} Constructed. activeSceneName={SceneManager.GetActiveScene().name}, currentScene={currentScene}, initialState={currentState}");
        }

        public ISceneTransitionService.StartResult RequestStartTransition(AppScene nextScene) {
            Debug.Log($"{LOG_PREFIX} RequestStartTransition called. currentState={currentState}, currentScene={currentScene}, nextScene={nextScene}");
            if (currentState != TransitionState.Idle) {
                Debug.LogWarning($"Cannot start transition to {nextScene} because current state is {currentState}");
                return new ISceneTransitionService.StartResult(false);
            }

            var request = new AppTransitionRequest(currentScene, nextScene);
            currentTransition = transitionFactory.Create(request);
            Debug.Log($"{LOG_PREFIX} RequestStartTransition created transition presenter={currentTransition != null}");

            currentState = TransitionState.Exiting;
            Debug.Log($"{LOG_PREFIX} State changed to {currentState}");
            _ = RunStartTransitionAsync(nextScene);

            return new ISceneTransitionService.StartResult(true);
        }

        async Task RunStartTransitionAsync(AppScene nextScene) {
            try {
                Debug.Log($"{LOG_PREFIX} RunStartTransitionAsync begin. nextScene={nextScene}");
                await currentTransition.PresentTransitionOut(new TransitionContext());
                Debug.Log($"{LOG_PREFIX} TransitionOut completed for nextScene={nextScene}");


                currentState = TransitionState.Loading;
                Debug.Log($"{LOG_PREFIX} State changed to {currentState}");
                await sceneLoader.LoadAsync(nextScene);
                Debug.Log($"Scene {nextScene} loaded".ToCyan());

                currentScene = nextScene;
                appBgmPlayer.Play(screenRegistry.GetByScene(nextScene).Bgm);
                
                currentState = TransitionState.Ready;
                Debug.Log($"{LOG_PREFIX} RunStartTransitionAsync completed. currentScene={currentScene}, state={currentState}");
            }
            catch (Exception ex) {
                Debug.LogError($"{LOG_PREFIX} RunStartTransitionAsync failed: {ex.Message}");
                Debug.LogException(ex);
                currentTransition?.DestroyGameObject();
                currentTransition = null;
                currentState = TransitionState.Idle;
                Debug.Log($"{LOG_PREFIX} RunStartTransitionAsync fallback to state={currentState}");
            }
        }

        public async Task<ISceneTransitionService.ExitResult> RequestEndTransitionAsync(AppScene scene) {
            Debug.Log($"{LOG_PREFIX} RequestEndTransitionAsync called. requestedScene={scene}, currentScene={currentScene}, state={currentState}");
            await WaitForReadyOrIdleAsync();
            Debug.Log($"{LOG_PREFIX} RequestEndTransitionAsync resumed after wait. state={currentState}");

            if (currentState == TransitionState.Idle) {
                Debug.Log($"{LOG_PREFIX} RequestEndTransitionAsync skipped because state is Idle");
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
                Debug.Log($"{LOG_PREFIX} State changed to {currentState}. PresentTransitionIn begin");
                await currentTransition.PresentTransitionIn(new TransitionContext());
                Debug.Log($"{LOG_PREFIX} PresentTransitionIn completed. destroying transition presenter");

                currentTransition.DestroyGameObject();
                currentTransition = null;

                currentState = TransitionState.Idle;
                Debug.Log($"{LOG_PREFIX} RequestEndTransitionAsync completed successfully. state={currentState}");

                return new ISceneTransitionService.ExitResult(true);
            }
            catch (Exception ex) {
                Debug.LogError($"{LOG_PREFIX} RequestEndTransitionAsync failed: {ex.Message}");
                Debug.LogException(ex);
                currentState = TransitionState.Idle;
                Debug.Log($"{LOG_PREFIX} RequestEndTransitionAsync fallback to state={currentState}");
                return new ISceneTransitionService.ExitResult(false);
            }
        }

        async Task WaitForReadyOrIdleAsync() {
            var frameCount = 0;
            while (currentState == TransitionState.Exiting || currentState == TransitionState.Loading) {
                if (frameCount % 120 == 0) {
                    Debug.Log($"{LOG_PREFIX} WaitForReadyOrIdleAsync waiting... frameCount={frameCount}, state={currentState}");
                }
                frameCount += 1;
                await Task.Yield();
            }
            Debug.Log($"{LOG_PREFIX} WaitForReadyOrIdleAsync exit. waitedFrames={frameCount}, state={currentState}");
        }
    }
}
