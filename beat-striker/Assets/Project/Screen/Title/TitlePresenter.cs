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
            Confirming,
            Transitioning,
        }

        readonly TitleScene view;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IGamePadRegistry gamePadRegistry;
        readonly ITutorialSetting tutorialSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly PlayerSession playerSession;
        readonly IPlayerProfileService profiles;
        readonly ChoiceDialogPresenter playerDialog;
        bool disposed;
        readonly CompositeDisposable subscriptions = new();
        bool quitRequested;
        TitleInputState inputState = TitleInputState.Transitioning;

        [Inject]
        public TitlePresenter(
            TitleScene view,
            ISceneTransitionService sceneTransitionService,
            IGamePadRegistry gamePadRegistry,
            ITutorialSetting tutorialSetting,
            IAppNetworkSetting appNetworkSetting,
            PlayerSession playerSession,
            IPlayerProfileService profiles) {
            this.view = view;
            this.sceneTransitionService = sceneTransitionService;
            this.gamePadRegistry = gamePadRegistry;
            this.tutorialSetting = tutorialSetting;
            this.appNetworkSetting = appNetworkSetting;
            this.playerSession = playerSession; this.profiles = profiles;
            playerDialog = new ChoiceDialogPresenter(view.PlayerDialog, playerSession, gamePadRegistry);
            view.OnlineRequested.Subscribe(ignored => { _ = ConfirmPlayersAsync(AppScene.OnlineRoom); }).AddTo(subscriptions);
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
            gamePadRegistry.RestoreOfflinePrimaryLayout(appNetworkSetting.LocalOnlinePlayerId);
            appNetworkSetting.SetLocalOnlinePlayerId(0);
            tutorialSetting.ClearTutorialBattleRequest();
            playerSession.SetMode(BattlePlayMode.Local);
            Debug.Log($"{LOG_PREFIX} EnterTitleAsync requesting end transition. scene={AppScene.Title}");
            var result = await sceneTransitionService.RequestEndTransitionAsync(AppScene.Title);
            if (!disposed) inputState = TitleInputState.Ready;
            Debug.Log($"{LOG_PREFIX} EnterTitleAsync end transition completed. isSuccess={result.IsSuccess}");
        }

        void RequestTransitionToMenu() {
            if (inputState != TitleInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu ignored because inputState={inputState}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu requesting start transition. nextScene={AppScene.Menu}");
            _ = ConfirmPlayersAsync(AppScene.Menu);
        }

        async Task ConfirmPlayersAsync(AppScene target) {
            if (inputState != TitleInputState.Ready) return;
            inputState = TitleInputState.Confirming;
            try {
                for (var slot = 0; slot < 4; slot++) {
                    if (target == AppScene.OnlineRoom && slot > 0) break;
                    if (slot > 0 && !gamePadRegistry.Get(slot).HasGamePad.CurrentValue) continue;
                    var name = playerSession.GetName(slot);
                    var editing = string.IsNullOrWhiteSpace(name);
                    while (!disposed) {
                        if (editing) {
                            var result = await playerDialog.AskAsync($"{slot + 1}P 名前入力", "名前を1〜10文字で入力してください", new[] { "決定", "戻る" }, name ?? "");
                            if (result == 1) return;
                            if (!PlayerProfileService.TryNormalizeName(playerDialog.InputText, out name)) continue;
                            await profiles.SelectNameAsync(slot, name);
                            if (disposed) return;
                            editing = false;
                        }
                        var profile = profiles.Get(name);
                        var choice = await playerDialog.AskAsync($"{slot + 1}P プレイヤー確認", $"{name}\n{PlayerGrades.Label((PlayerGrade)profile.grade)}", new[] { "OK", "編集", "戻る" });
                        if (choice == 2) return;
                        if (choice == 1) { editing = true; continue; }
                        break;
                    }
                }
                if (disposed) return;
                playerSession.SetMode(target == AppScene.OnlineRoom ? BattlePlayMode.Online : BattlePlayMode.Local);
                RequestTransition(target);
            } catch (System.OperationCanceledException) { }
            catch (System.Exception exception) {
                Debug.LogError($"{LOG_PREFIX} Player confirmation failed: {exception.Message}");
                if (!disposed) {
                    try { await playerDialog.AskAsync("名前を保存できませんでした", "保存先を確認して、もう一度お試しください。", new[] { "OK" }); }
                    catch (System.OperationCanceledException) { }
                }
            }
            finally { if (inputState == TitleInputState.Confirming) inputState = TitleInputState.Ready; }
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
            disposed = true; playerDialog.Dispose();
            subscriptions.Dispose();
        }
    }
}
