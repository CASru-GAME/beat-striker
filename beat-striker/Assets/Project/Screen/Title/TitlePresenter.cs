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
            TutorialDialogOpen,
            Transitioning,
        }

        readonly TitleScene view;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly ITutorialSetting tutorialSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly CompositeDisposable subscriptions = new();
        bool quitRequested;
        TitleInputState inputState = TitleInputState.Ready;

        [Inject]
        public TitlePresenter(
            TitleScene view,
            ISceneTransitionService sceneTransitionService,
            IGamePadRegistry gamePadRegistry,
            IPlayerSelectSetting playerSelectSetting,
            IBattleSelectSetting battleSelectSetting,
            ITutorialSetting tutorialSetting,
            IAppNetworkSetting appNetworkSetting) {
            this.view = view;
            this.sceneTransitionService = sceneTransitionService;
            this.gamePadRegistry = gamePadRegistry;
            this.playerSelectSetting = playerSelectSetting;
            this.battleSelectSetting = battleSelectSetting;
            this.tutorialSetting = tutorialSetting;
            this.appNetworkSetting = appNetworkSetting;
            Debug.Log($"{LOG_PREFIX} Constructed and subscribing view events");

            this.gamePadRegistry.OnAnyButtonDown
                .Where(e => e.Button == GamePadButton.Select)
                .Subscribe(_ => RotateFaceButtonWiring())
                .AddTo(subscriptions);

            this.view.GotoSelectRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} GotoSelectRequested received");
                    OpenTutorialStartDialog();
                })
                .AddTo(subscriptions);
            this.view.TutorialBattleAccepted
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} TutorialBattleAccepted received");
                    appNetworkSetting.SetIsOnline(false);
                    StartTutorialBattle();
                })
                .AddTo(subscriptions);
            this.view.TutorialBattleDeclined
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} TutorialBattleDeclined received");
                    appNetworkSetting.SetIsOnline(false);
                    GoToSelectScene();
                })
                .AddTo(subscriptions);
            this.view.OnlineBattleRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} OnlineBattleRequested received");
                    appNetworkSetting.SetIsOnline(true);
                    GoToSelectScene();
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
            appNetworkSetting.SetIsOnline(false);
            tutorialSetting.ClearTutorialBattleRequest();
            Debug.Log($"{LOG_PREFIX} EnterTitleAsync end transition completed. isSuccess={result.IsSuccess}");
        }

        void OpenTutorialStartDialog() {
            if (inputState != TitleInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} OpenTutorialStartDialog ignored because inputState={inputState}");
                return;
            }

            inputState = TitleInputState.TutorialDialogOpen;
            view.OpenTutorialStartDialog();
            Debug.Log($"{LOG_PREFIX} OpenTutorialStartDialog opened. inputState={inputState}");
        }

        void StartTutorialBattle() {
            if (inputState != TitleInputState.TutorialDialogOpen) {
                Debug.Log($"{LOG_PREFIX} StartTutorialBattle ignored because inputState={inputState}");
                return;
            }

            var tutorialSelection = tutorialSetting.BattleSelection;
            tutorialSetting.RequestTutorialBattle();
            battleSelectSetting.SelectStage(tutorialSelection.Stage);
            battleSelectSetting.SelectMusic(tutorialSelection.MusicId);

            playerSelectSetting.ResetSelections();
            playerSelectSetting.SelectStriker(0, Striker.Warrior);
            for (var i = 0; i < tutorialSelection.PlayerSelections.Count; i++) {
                var selection = tutorialSelection.PlayerSelections[i];
                if (selection.PlayerId == 0) {
                    continue;
                }

                playerSelectSetting.SelectStriker(selection.PlayerId, selection.Striker);
            }

            var nextScene = ResolveBattleScene(tutorialSelection.Stage);
            RequestTransition(nextScene);
        }

        public void GoToSelectScene() {
            if (inputState != TitleInputState.TutorialDialogOpen && inputState != TitleInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} GoToSelectScene ignored because inputState={inputState}");
                return;
            }

            tutorialSetting.ClearTutorialBattleRequest();
            view.CloseTutorialStartDialog();
            Debug.Log($"{LOG_PREFIX} GoToSelectScene requesting start transition. nextScene={AppScene.StageSelect}");
            RequestTransition(AppScene.StageSelect);
        }

        AppScene ResolveBattleScene(Stage stage) {
            return stage == Stage.Street
                ? AppScene.Street
                : AppScene.Live;
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
