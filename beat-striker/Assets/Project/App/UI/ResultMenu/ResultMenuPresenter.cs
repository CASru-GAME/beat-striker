using System.Threading.Tasks;
using UnityEngine;

namespace Alice {
    public class ResultMenuPresenter {
        const string LOG_PREFIX = "[ResultMenuPresenter]";

        readonly ISceneTransitionService sceneTransitionService;
        bool initialized;

        public ResultMenuPresenter(ISceneTransitionService sceneTransitionService) {
            this.sceneTransitionService = sceneTransitionService;
            Initialize();
        }

        void Initialize() {
            if (initialized) {
                Debug.Log($"{LOG_PREFIX} Initialize skipped because already initialized");
                return;
            }

            Debug.Log($"{LOG_PREFIX} Initialize start");
            _ = EnterResultMenuAsync();
            initialized = true;
            Debug.Log($"{LOG_PREFIX} Initialize completed");
        }

        async Task EnterResultMenuAsync() {
            Debug.Log($"{LOG_PREFIX} EnterResultMenuAsync requesting end transition. scene={AppScene.ResultMenu}");
            var result = await sceneTransitionService.RequestEndTransitionAsync(AppScene.ResultMenu);
            Debug.Log($"{LOG_PREFIX} EnterResultMenuAsync completed. isSuccess={result.IsSuccess}");
        }
    }
}
