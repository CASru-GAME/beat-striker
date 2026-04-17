using R3;
using System.Threading.Tasks;
using UnityEngine;

namespace Alice {
    public class BackScenePresenter : System.IDisposable {
        const string LOG_PREFIX = "[BackScenePresenter]";

        readonly BackSelectSceneTextHover[] views;
        readonly ISceneTransitionService transitionService;
        readonly CompositeDisposable subscriptions = new();
        bool initialized;

        public BackScenePresenter(
            BackSelectSceneTextHover[] views,
            ISceneTransitionService transitionService) {
            this.views = views;
            this.transitionService = transitionService;

            Initialize();
        }

        void Initialize() {
            if (initialized) {
                Debug.Log($"{LOG_PREFIX} Initialize skipped because already initialized");
                return;
            }

            Debug.Log($"{LOG_PREFIX} Initialize start. viewCount={views.Length}");

            for (var i = 0; i < views.Length; i++) {
                var index = i;
                views[i].OnClicked
                    .Subscribe(scene => {
                        Debug.Log($"{LOG_PREFIX} OnClicked received. requesting start transition. nextScene={scene}, viewIndex={index}");
                        var result = transitionService.RequestStartTransition(scene);
                        Debug.Log($"{LOG_PREFIX} OnClicked transition request result. isSuccess={result.IsSuccess}, nextScene={scene}");
                    })
                    .AddTo(subscriptions);
            }

            _ = EnterBackSceneAsync();
            initialized = true;
            Debug.Log($"{LOG_PREFIX} Initialize completed");
        }

        async Task EnterBackSceneAsync() {
            Debug.Log($"{LOG_PREFIX} EnterBackSceneAsync requesting end transition. scene={AppScene.ResultMenu}");
            var result = await transitionService.RequestEndTransitionAsync(AppScene.ResultMenu);
            Debug.Log($"{LOG_PREFIX} EnterBackSceneAsync completed. isSuccess={result.IsSuccess}");
        }

        public void Dispose() {
            subscriptions.Dispose();
        }
    }
}
