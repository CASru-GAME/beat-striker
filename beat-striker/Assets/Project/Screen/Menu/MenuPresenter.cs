using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class MenuPresenter : System.IDisposable {
        const string LOG_PREFIX = "[MenuPresenter]";

        enum MenuInputState {
            Ready,
            Transitioning,
        }

        readonly MenuPresenterView view;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly ITutorialSetting tutorialSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly CompositeDisposable subscriptions = new();
        MenuInputState inputState = MenuInputState.Ready;

        [Inject]
        public MenuPresenter(
            MenuPresenterView view,
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

            this.view.GotoTitleRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} GotoTitleRequested received");
                    RequestTransitionToTitle();
                })
                .AddTo(subscriptions);

            this.view.GotoTutorialRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} GotoTutorialRequested received");
                    StartTutorialBattle();
                })
                .AddTo(subscriptions);

            this.view.LocalBattleRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} LocalBattleRequested received");
                    GoToStageSelect();
                })
                .AddTo(subscriptions);

            this.view.OnlineBattleRequested
                .Subscribe(_ => {
                    if (inputState != MenuInputState.Ready) return;
                    tutorialSetting.ClearTutorialBattleRequest();
                    RequestTransition(AppScene.OnlineRoom);
                })
                .AddTo(subscriptions);

            this.view.GotoRankingRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} GotoRankingRequested received");
                    RequestTransitionToRanking();
                })
                .AddTo(subscriptions);

            _ = EnterMenuAsync();
        }

        async Task EnterMenuAsync() {
            gamePadRegistry.RestoreOfflinePrimaryLayout(appNetworkSetting.LocalOnlinePlayerId);
            appNetworkSetting.SetLocalOnlinePlayerId(0);
            tutorialSetting.ClearTutorialBattleRequest();
            Debug.Log($"{LOG_PREFIX} EnterMenuAsync requesting end transition. scene={AppScene.Menu}");
            var result = await sceneTransitionService.RequestEndTransitionAsync(AppScene.Menu);
            Debug.Log($"{LOG_PREFIX} EnterMenuAsync end transition completed. isSuccess={result.IsSuccess}");
        }

        void RequestTransitionToTitle() {
            if (inputState != MenuInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} RequestTransitionToTitle ignored because inputState={inputState}");
                return;
            }

            tutorialSetting.ClearTutorialBattleRequest();
            Debug.Log($"{LOG_PREFIX} RequestTransitionToTitle requesting start transition. nextScene={AppScene.Title}");
            RequestTransition(AppScene.Title);
        }

        void GoToStageSelect() {
            if (inputState != MenuInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} GoToStageSelect ignored because inputState={inputState}");
                return;
            }

            tutorialSetting.ClearTutorialBattleRequest();
            Debug.Log($"{LOG_PREFIX} GoToStageSelect requesting start transition. nextScene={AppScene.StageSelect}");
            RequestTransition(AppScene.StageSelect);
        }

        void RequestTransitionToRanking() {
            if (inputState != MenuInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} RequestTransitionToRanking ignored because inputState={inputState}");
                return;
            }

            tutorialSetting.ClearTutorialBattleRequest();
            Debug.Log($"{LOG_PREFIX} RequestTransitionToRanking requesting start transition. nextScene={AppScene.Ranking}");
            RequestTransition(AppScene.Ranking);
        }

        void StartTutorialBattle() {
            if (inputState != MenuInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} StartTutorialBattle ignored because inputState={inputState}");
                return;
            }

            var tutorialSelection = tutorialSetting.BattleSelection;
            tutorialSetting.RequestTutorialBattle();
            battleSelectSetting.SelectStage(tutorialSelection.Stage);
            battleSelectSetting.SelectMusic(tutorialSelection.MusicId);

            RequestTransition(AppScene.CharacterSelect);
        }

        void RequestTransition(AppScene nextScene) {
            inputState = MenuInputState.Transitioning;
            var result = sceneTransitionService.RequestStartTransition(nextScene);
            Debug.Log($"{LOG_PREFIX} RequestTransition result. nextScene={nextScene}, isSuccess={result.IsSuccess}");
            if (result.IsSuccess) {
                return;
            }

            inputState = MenuInputState.Ready;
        }

        public void Dispose() {
            subscriptions.Dispose();
        }
    }
}
