using System.Threading.Tasks;
using R3;
using UnityEngine;

namespace Alice {
    public class TitlePresenter : System.IDisposable {
        const string LOG_PREFIX = "[TitlePresenter]";

        readonly TitleScene view;
        readonly ISceneTransitionService sceneTransitionService;
        readonly CompositeDisposable subscriptions = new();

        public TitlePresenter(TitleScene view, ISceneTransitionService sceneTransitionService) {
            this.view = view;
            this.sceneTransitionService = sceneTransitionService;
            Debug.Log($"{LOG_PREFIX} Constructed and subscribing view events");

            this.view.GotoSelectRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} GotoSelectRequested received");
                    GoToSelectScene();
                })
                .AddTo(subscriptions);
            _ = EnterTitleAsync();
        }

        public async Task EnterTitleAsync() {
            Debug.Log($"{LOG_PREFIX} EnterTitleAsync requesting end transition. scene={AppScene.Title}");
            var result = await sceneTransitionService.RequestEndTransitionAsync(AppScene.Title);
            Debug.Log($"{LOG_PREFIX} EnterTitleAsync end transition completed. isSuccess={result.IsSuccess}");
        }

        public void GoToSelectScene() {
            Debug.Log($"{LOG_PREFIX} GoToSelectScene requesting start transition. nextScene={AppScene.StageSelect}");
            var result = sceneTransitionService.RequestStartTransition(AppScene.StageSelect);
            Debug.Log($"{LOG_PREFIX} GoToSelectScene start transition result. isSuccess={result.IsSuccess}");
        }

        public void Dispose() {
            subscriptions.Dispose();
        }
    }
}
