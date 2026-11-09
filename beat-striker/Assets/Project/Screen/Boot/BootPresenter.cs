using System;
using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;

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
        readonly IInputFocusService inputFocus;
        readonly CompositeDisposable subscriptions = new();

        BootInputState inputState = BootInputState.Entering;
        bool disposed;

        [Inject]
        public BootPresenter(
            BootScene view,
            IAppUISetting appUISetting,
            ISceneTransitionService sceneTransitionService,
            IInputFocusService inputFocus) {
            this.view = view;
            this.appUISetting = appUISetting;
            this.sceneTransitionService = sceneTransitionService;
            this.inputFocus = inputFocus;

            view.TouchControllerSelectionRequested
                .Subscribe(OnTouchControllerSelectionRequested)
                .AddTo(subscriptions);

            _ = EnterBootAsync();
        }

        async Task EnterBootAsync() {
            using (inputFocus.Block()) {
                Debug.Log($"{LOG_PREFIX} EnterBootAsync requesting end transition. scene={AppScene.Boot}");
                var result = await sceneTransitionService.RequestEndTransitionAsync(AppScene.Boot);
                if (disposed) return;

                try {
                    await view.PlayCreditsAsync();
                }
                catch (OperationCanceledException) {
                    return;
                }
                Debug.Log($"{LOG_PREFIX} EnterBootAsync completed. isSuccess={result.IsSuccess}");
            }
            if (disposed) return;
            inputState = BootInputState.Ready;
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
            disposed = true;
            subscriptions.Dispose();
        }
    }
}
