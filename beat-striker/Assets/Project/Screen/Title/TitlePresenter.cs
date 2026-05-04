using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Alice {
    public class TitlePresenter : System.IDisposable {
        const string LOG_PREFIX = "[TitlePresenter]";

        enum TitleInputState {
            Ready,
            Transitioning,
        }

        readonly TitleScene view;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IGamePadRegistry gamePadRegistry;
        readonly ITutorialSetting tutorialSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IOnlineSessionBootstrap onlineSessionBootstrap;
        readonly CompositeDisposable subscriptions = new();
        bool quitRequested;
        TitleInputState inputState = TitleInputState.Ready;

        [Inject]
        public TitlePresenter(
            TitleScene view,
            ISceneTransitionService sceneTransitionService,
            IGamePadRegistry gamePadRegistry,
            ITutorialSetting tutorialSetting,
            IAppNetworkSetting appNetworkSetting,
            IOnlineSessionBootstrap onlineSessionBootstrap) {
            this.view = view;
            this.sceneTransitionService = sceneTransitionService;
            this.gamePadRegistry = gamePadRegistry;
            this.tutorialSetting = tutorialSetting;
            this.appNetworkSetting = appNetworkSetting;
            this.onlineSessionBootstrap = onlineSessionBootstrap;
            Debug.Log($"{LOG_PREFIX} Constructed and subscribing view events");

            this.gamePadRegistry.OnAnyButtonDown
                .Where(e => e.Button == GamePadButton.Select)
                .Subscribe(_ => RotateFaceButtonWiring())
                .AddTo(subscriptions);

            this.view.GotoSelectRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} GotoSelectRequested received");
                    RequestTransitionToMenu();
                })
                .AddTo(subscriptions);
            this.view.GotoSettingsRequested
                .Subscribe(_ => {
                    if (inputState != TitleInputState.Ready) {
                        return;
                    }

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
            gamePadRegistry.RestoreOfflinePrimaryLayout(appNetworkSetting.LocalOnlinePlayerId);
            await onlineSessionBootstrap.TeardownOnlineRunnerAsync();
            appNetworkSetting.SetIsOnline(false);
            appNetworkSetting.SetLocalOnlinePlayerId(0);
            tutorialSetting.ClearTutorialBattleRequest();
            Debug.Log($"{LOG_PREFIX} EnterTitleAsync end transition completed. isSuccess={result.IsSuccess}");
        }

        void RequestTransitionToMenu() {
            if (inputState != TitleInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu ignored because inputState={inputState}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu requesting start transition. nextScene={AppScene.Menu}");
            RequestTransition(AppScene.Menu);
        }

        void RequestTransition(AppScene nextScene) {
            inputState = TitleInputState.Transitioning;
            var result = sceneTransitionService.RequestStartTransition(nextScene);
            Debug.Log($"{LOG_PREFIX} RequestTransition result. nextScene={nextScene}, isSuccess={result.IsSuccess}");
            if (result.IsSuccess) {
                return;
            }

            inputState = TitleInputState.Ready;
        }

        void RotateFaceButtonWiring() {
            if (inputState != TitleInputState.Ready) {
                return;
            }

            gamePadRegistry.RotateFaceButtonWiringClockwise();
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
