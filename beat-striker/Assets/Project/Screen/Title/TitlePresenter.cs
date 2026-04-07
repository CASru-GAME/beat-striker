using System.Threading.Tasks;
using R3;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Alice {
    public class TitlePresenter : System.IDisposable {
        const string LOG_PREFIX = "[TitlePresenter]";

        readonly TitleScene view;
        readonly ISceneTransitionService sceneTransitionService;
        readonly CompositeDisposable subscriptions = new();
        bool quitRequested;

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
            this.view.GotoSettingsRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} GotoSettingsRequested received");
                    view.OpenSettingsDialog();
                })
                .AddTo(subscriptions);
            this.view.QuitRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} QuitRequested received");
                    QuitGame();
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

        void QuitGame() {
            if (quitRequested) {
                Debug.LogWarning($"{LOG_PREFIX} QuitGame ignored because quit is already requested");
                return;
            }

            quitRequested = true;
            Debug.Log($"{LOG_PREFIX} QuitGame executing");

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Dispose() {
            subscriptions.Dispose();
        }
    }
}
