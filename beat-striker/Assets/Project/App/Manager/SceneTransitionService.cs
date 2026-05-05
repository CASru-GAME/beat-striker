using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Alice {

    public interface ISceneTransitionService {
        bool IsTransitioning { get; }
        Observable<bool> TransitioningChanged { get; }
        Observable<AppScene> EndTransitionCompleted { get; }
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
        readonly Subject<bool> transitioningChanged = new();
        readonly Subject<AppScene> endTransitionCompleted = new();

        TransitionState currentState = TransitionState.Idle;
        AppScene currentScene;
        IAppTransitionPresenter currentTransition;
        int startRequestSequence;
        int endRequestSequence;
        int activeTransitionId;

        enum TransitionState {
            Ready,
            Entering,
            Idle,
            Exiting,
            Loading,
        }

        public bool IsTransitioning => currentState != TransitionState.Idle;
        public Observable<bool> TransitioningChanged => transitioningChanged;
        public Observable<AppScene> EndTransitionCompleted => endTransitionCompleted;

        [Inject]
        public SceneTransitionService(
            ISceneLoader sceneLoader,
            IAppTransitionFactory transitionFactory,
            IScreenRegistry screenRegistry,
            IAppBGMPlayer appBgmPlayer) {
            this.sceneLoader = sceneLoader;
            this.transitionFactory = transitionFactory;
            this.screenRegistry = screenRegistry;
            this.appBgmPlayer = appBgmPlayer;

            var activeSceneName = SceneManager.GetActiveScene().name;
            if (!screenRegistry.TryGetBySceneName(activeSceneName, out var currentScreen)) {
                currentScreen = screenRegistry.Default;
                Debug.LogWarning($"{LOG_PREFIX} Active scene is not registered. sceneName={activeSceneName}, fallbackScene={currentScreen.SceneName}");
            }

            currentScene = currentScreen.Scene;
            appBgmPlayer.Play(currentScreen.Bgm);
            Debug.Log($"{LOG_PREFIX} Constructed. activeSceneName={SceneManager.GetActiveScene().name}, currentScene={currentScene}, initialState={currentState}");
        }

        public ISceneTransitionService.StartResult RequestStartTransition(AppScene nextScene) {
            startRequestSequence += 1;
            var requestId = startRequestSequence;
            var callerHint = ResolveCallerHint();
            Debug.Log($"{LOG_PREFIX} [START#{requestId}] RequestStartTransition called. currentState={currentState}, currentScene={currentScene}, nextScene={nextScene}, caller={callerHint}");
            if (currentState != TransitionState.Idle) {
                Debug.LogWarning($"{LOG_PREFIX} [START#{requestId}] Rejected. nextScene={nextScene}, currentState={currentState}, caller={callerHint}");
                return new ISceneTransitionService.StartResult(false);
            }

            var request = new AppTransitionRequest(currentScene, nextScene);
            currentTransition = transitionFactory.Create(request);
            activeTransitionId = requestId;
            Debug.Log($"{LOG_PREFIX} [START#{requestId}] Transition presenter created. hasPresenter={currentTransition != null}");

            SetTransitionState(TransitionState.Exiting);
            Debug.Log($"{LOG_PREFIX} [START#{requestId}] State changed to {currentState}");
            _ = RunStartTransitionAsync(nextScene, requestId);

            return new ISceneTransitionService.StartResult(true);
        }

        async Task RunStartTransitionAsync(AppScene nextScene, int requestId) {
            try {
                Debug.Log($"{LOG_PREFIX} [START#{requestId}] RunStartTransitionAsync begin. nextScene={nextScene}");
                await currentTransition.PresentTransitionOut(new TransitionContext());
                Debug.Log($"{LOG_PREFIX} [START#{requestId}] TransitionOut completed. nextScene={nextScene}");


                SetTransitionState(TransitionState.Loading);
                Debug.Log($"{LOG_PREFIX} [START#{requestId}] State changed to {currentState}");
                await sceneLoader.LoadAsync(nextScene);
                Debug.Log($"{LOG_PREFIX} [START#{requestId}] Scene loaded. nextScene={nextScene}".ToCyan());

                currentScene = nextScene;
                appBgmPlayer.Play(screenRegistry.GetByScene(nextScene).Bgm);
                
                SetTransitionState(TransitionState.Ready);
                Debug.Log($"{LOG_PREFIX} [START#{requestId}] RunStartTransitionAsync completed. currentScene={currentScene}, state={currentState}");
            }
            catch (Exception ex) {
                Debug.LogError($"{LOG_PREFIX} [START#{requestId}] RunStartTransitionAsync failed: {ex.Message}");
                Debug.LogException(ex);
                currentTransition?.DestroyGameObject();
                currentTransition = null;
                SetTransitionState(TransitionState.Idle);
                Debug.Log($"{LOG_PREFIX} [START#{requestId}] RunStartTransitionAsync fallback to state={currentState}");
            }
        }

        public async Task<ISceneTransitionService.ExitResult> RequestEndTransitionAsync(AppScene scene) {
            endRequestSequence += 1;
            var requestId = endRequestSequence;
            var callerHint = ResolveCallerHint();
            Debug.Log($"{LOG_PREFIX} [END#{requestId}] RequestEndTransitionAsync called. requestedScene={scene}, currentScene={currentScene}, state={currentState}, activeTransitionId={activeTransitionId}, caller={callerHint}");
            await WaitForReadyOrIdleAsync(requestId, scene);
            Debug.Log($"{LOG_PREFIX} [END#{requestId}] RequestEndTransitionAsync resumed after wait. state={currentState}, activeTransitionId={activeTransitionId}");

            if (currentState == TransitionState.Idle) {
                Debug.Log($"{LOG_PREFIX} [END#{requestId}] RequestEndTransitionAsync skipped because state is Idle");
                endTransitionCompleted.OnNext(scene);
                return new ISceneTransitionService.ExitResult(true);
            }

            if (currentState != TransitionState.Ready) {
                Debug.LogWarning($"{LOG_PREFIX} [END#{requestId}] Rejected. requestedScene={scene}, currentState={currentState}");
                return new ISceneTransitionService.ExitResult(false);
            }

            if (currentTransition == null) {
                Debug.LogWarning($"{LOG_PREFIX} [END#{requestId}] No transition presenter. requestedScene={scene}. forcing idle");
                SetTransitionState(TransitionState.Idle);
                endTransitionCompleted.OnNext(scene);
                return new ISceneTransitionService.ExitResult(true);
            }
            
            try {
                SetTransitionState(TransitionState.Entering);
                Debug.Log($"{LOG_PREFIX} [END#{requestId}] State changed to {currentState}. PresentTransitionIn begin");
                await currentTransition.PresentTransitionIn(new TransitionContext());
                Debug.Log($"{LOG_PREFIX} [END#{requestId}] PresentTransitionIn completed. destroying transition presenter");

                currentTransition.DestroyGameObject();
                currentTransition = null;

                SetTransitionState(TransitionState.Idle);
                activeTransitionId = 0;
                Debug.Log($"{LOG_PREFIX} [END#{requestId}] RequestEndTransitionAsync completed successfully. state={currentState}");
                endTransitionCompleted.OnNext(scene);

                return new ISceneTransitionService.ExitResult(true);
            }
            catch (Exception ex) {
                Debug.LogError($"{LOG_PREFIX} [END#{requestId}] RequestEndTransitionAsync failed: {ex.Message}");
                Debug.LogException(ex);
                SetTransitionState(TransitionState.Idle);
                Debug.Log($"{LOG_PREFIX} [END#{requestId}] RequestEndTransitionAsync fallback to state={currentState}");
                return new ISceneTransitionService.ExitResult(false);
            }
        }

        void SetTransitionState(TransitionState nextState) {
            var wasTransitioning = IsTransitioning;
            currentState = nextState;
            var isTransitioning = IsTransitioning;
            if (wasTransitioning != isTransitioning) {
                transitioningChanged.OnNext(isTransitioning);
            }
        }

        async Task WaitForReadyOrIdleAsync(int requestId, AppScene requestedScene) {
            var frameCount = 0;
            var waitStartTime = Time.realtimeSinceStartup;
            while (currentState == TransitionState.Exiting || currentState == TransitionState.Loading) {
                if (frameCount % 120 == 0) {
                    var elapsed = Time.realtimeSinceStartup - waitStartTime;
                    Debug.Log($"{LOG_PREFIX} [END#{requestId}] WaitForReadyOrIdleAsync waiting... requestedScene={requestedScene}, frameCount={frameCount}, elapsed={elapsed:F2}s, state={currentState}, currentScene={currentScene}, activeTransitionId={activeTransitionId}");
                }
                if (frameCount > 0 && frameCount % 600 == 0) {
                    var elapsed = Time.realtimeSinceStartup - waitStartTime;
                    Debug.LogWarning($"{LOG_PREFIX} [END#{requestId}] WaitForReadyOrIdleAsync long wait detected. requestedScene={requestedScene}, frameCount={frameCount}, elapsed={elapsed:F2}s, state={currentState}, currentScene={currentScene}, activeTransitionId={activeTransitionId}");
                }
                frameCount += 1;
                await Task.Yield();
            }
            var totalElapsed = Time.realtimeSinceStartup - waitStartTime;
            Debug.Log($"{LOG_PREFIX} [END#{requestId}] WaitForReadyOrIdleAsync exit. waitedFrames={frameCount}, elapsed={totalElapsed:F2}s, state={currentState}, currentScene={currentScene}, activeTransitionId={activeTransitionId}");
        }

        static string ResolveCallerHint() {
            var stackLines = Environment.StackTrace.Split('\n');
            for (var i = 0; i < stackLines.Length; i++) {
                var line = stackLines[i].Trim();
                if (line.Contains("Presenter") || line.Contains("Flow")) {
                    return line;
                }
            }

            if (stackLines.Length > 3) {
                return stackLines[3].Trim();
            }

            return "UnknownCaller";
        }
    }
}
