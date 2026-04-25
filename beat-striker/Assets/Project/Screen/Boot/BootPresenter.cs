using System;
using System.Threading.Tasks;
using R3;
using UnityEngine;

namespace Alice {
    public class BootPresenter : IDisposable {
        const string LOG_PREFIX = "[BootPresenter]";

        enum BootInputState {
            Entering,
            Ready,
            Transitioning,
        }

        readonly BootScene view;
        readonly IAppUISetting appUISetting;
        readonly ISceneTransitionService sceneTransitionService;
        readonly CompositeDisposable subscriptions = new();

        BootInputState inputState = BootInputState.Entering;

        public BootPresenter(
            BootScene view,
            IAppUISetting appUISetting,
            ISceneTransitionService sceneTransitionService) {
            this.view = view;
            this.appUISetting = appUISetting;
            this.sceneTransitionService = sceneTransitionService;

            view.TouchControllerSelectionRequested
                .Subscribe(OnTouchControllerSelectionRequested)
                .AddTo(subscriptions);

            _ = EnterBootAsync();
        }

        async Task EnterBootAsync() {
            Debug.Log($"{LOG_PREFIX} EnterBootAsync requesting end transition. scene={AppScene.Boot}");
            var result = await sceneTransitionService.RequestEndTransitionAsync(AppScene.Boot);
            inputState = BootInputState.Ready;
            Debug.Log($"{LOG_PREFIX} EnterBootAsync completed. isSuccess={result.IsSuccess}");
        }

        void OnTouchControllerSelectionRequested(bool usesTouchController) {
            if (inputState != BootInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} Selection ignored because inputState={inputState}");
                return;
            }

            inputState = BootInputState.Transitioning;
            appUISetting.SetUsesTouchController(usesTouchController);

            Debug.Log($"{LOG_PREFIX} Requesting start transition. nextScene={AppScene.Title}, usesTouchController={usesTouchController}");
            var result = sceneTransitionService.RequestStartTransition(AppScene.Title);
            Debug.Log($"{LOG_PREFIX} Start transition request completed. isSuccess={result.IsSuccess}");

            if (result.IsSuccess) {
                return;
            }

            inputState = BootInputState.Ready;
        }

        public void Dispose() {
            subscriptions.Dispose();
        }
    }
}
