using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class RankingPresenter : System.IDisposable {
        const string LOG_PREFIX = "[RankingPresenter]";

        enum RankingInputState {
            Ready,
            Transitioning,
        }

        readonly RankingPresenterView view;
        readonly ISceneTransitionService sceneTransitionService;
        readonly CompositeDisposable subscriptions = new();
        RankingInputState inputState = RankingInputState.Ready;

        [Inject]
        public RankingPresenter(RankingPresenterView view, ISceneTransitionService sceneTransitionService) {
            this.view = view;
            this.sceneTransitionService = sceneTransitionService;
            Debug.Log($"{LOG_PREFIX} Constructed and subscribing view events");

            this.view.BackToMenuRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} BackToMenuRequested received");
                    RequestTransitionToMenu();
                })
                .AddTo(subscriptions);

            _ = EnterRankingAsync();
        }

        async Task EnterRankingAsync() {
            Debug.Log($"{LOG_PREFIX} EnterRankingAsync requesting end transition. scene={AppScene.Ranking}");
            var result = await sceneTransitionService.RequestEndTransitionAsync(AppScene.Ranking);
            Debug.Log($"{LOG_PREFIX} EnterRankingAsync end transition completed. isSuccess={result.IsSuccess}");
        }

        void RequestTransitionToMenu() {
            if (inputState != RankingInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu ignored because inputState={inputState}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu requesting start transition. nextScene={AppScene.Menu}");
            RequestTransition(AppScene.Menu);
        }

        void RequestTransition(AppScene nextScene) {
            inputState = RankingInputState.Transitioning;
            var result = sceneTransitionService.RequestStartTransition(nextScene);
            Debug.Log($"{LOG_PREFIX} RequestTransition result. nextScene={nextScene}, isSuccess={result.IsSuccess}");
            if (result.IsSuccess) {
                return;
            }

            inputState = RankingInputState.Ready;
        }

        public void Dispose() {
            subscriptions.Dispose();
        }
    }
}
