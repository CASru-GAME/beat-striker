using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alice {

    public interface ISceneTransitionService {
        StartResult RequestStartTransition(AppScene nextScene);
        Task<ExitResult> RequestEndTransition(AppScene currentScene);

        public record StartResult(bool IsSuccess);
        public record ExitResult(bool IsSuccess);
    }

    public class SceneTransitionService : ISceneTransitionService {

        readonly ISceneLoader sceneLoader;
        readonly IAppTransitionFactory transitionFactory;

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

        public SceneTransitionService(ISceneLoader sceneLoader, IAppTransitionFactory transitionFactory) {
            this.sceneLoader = sceneLoader;
            this.transitionFactory = transitionFactory;
        }

        public ISceneTransitionService.StartResult RequestStartTransition(AppScene nextScene) {
            if (currentState != TransitionState.Idle) {
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

                currentScene = nextScene;
                
                currentState = TransitionState.Ready;
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                currentState = TransitionState.Idle;
            }
        }

        public async Task<ISceneTransitionService.ExitResult> RequestEndTransition(AppScene scene) {
            if (currentState != TransitionState.Ready) {
                return new ISceneTransitionService.ExitResult(false);
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
    }
}
