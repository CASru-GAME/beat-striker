using System;
using R3;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public interface IAppOverlayPresenter {
        bool IsOverlayVisible { get; }
        void HideDuelDialog();
    }

    public class AppOverlayPresenter : IAppOverlayPresenter, IInitializable, IDisposable {
        readonly AppOverlayView view;
        readonly IScreenRegistry screenRegistry;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IOnlineDuelFusionClient duelClient;
        readonly CompositeDisposable disposables = new();
        bool overlayEnabledForCurrentScreen;

        public bool IsOverlayVisible => overlayEnabledForCurrentScreen;

        [Inject]
        public AppOverlayPresenter(
            AppOverlayView view,
            IScreenRegistry screenRegistry,
            IAppNetworkSetting appNetworkSetting,
            IOnlineDuelFusionClient duelClient) {
            this.view = view;
            this.screenRegistry = screenRegistry;
            this.appNetworkSetting = appNetworkSetting;
            this.duelClient = duelClient;
        }

        public void Initialize() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyScreenRule(SceneManager.GetActiveScene().name);
            appNetworkSetting.IsOnline.Subscribe(_ => ApplyOnlineIndicatorFromNetwork()).AddTo(disposables);
            duelClient.State.Subscribe(ApplyDuelState).AddTo(disposables);
            view.IncomingDuelAccepted.Subscribe(_ => duelClient.AcceptInvite()).AddTo(disposables);
            view.IncomingDuelRejected.Subscribe(_ => duelClient.RejectInvite()).AddTo(disposables);
            view.CandidateDuelInvited.Subscribe(_ => duelClient.InviteCandidate()).AddTo(disposables);
            view.CandidateDuelSkipped.Subscribe(_ => duelClient.SkipCandidate()).AddTo(disposables);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            ApplyScreenRule(scene.name);
        }

        void ApplyScreenRule(string sceneName) {
            if (!screenRegistry.TryGetBySceneName(sceneName, out var screenInfo)) {
                screenInfo = screenRegistry.Default;
            }

            overlayEnabledForCurrentScreen = screenInfo.ShowAppOverlay;
            view.SetOverlayVisible(overlayEnabledForCurrentScreen);
            if (!overlayEnabledForCurrentScreen) {
                HideDuelDialog();
            }
            ApplyOnlineIndicatorFromNetwork();
            ApplyDuelState(duelClient.State.CurrentValue);
        }

        void ApplyOnlineIndicatorFromNetwork() {
            if (!overlayEnabledForCurrentScreen) {
                return;
            }

            view.SetOnlineIndicatorVisible(appNetworkSetting.IsOnline.CurrentValue);
        }

        void ApplyDuelState(OnlineDuelUiState state) {
            if (!overlayEnabledForCurrentScreen) {
                return;
            }

            view.SetIncomingDuelVisible(state.Phase == OnlineDuelPhase.IncomingInvite);
            view.SetCandidateDuelVisible(state.Phase == OnlineDuelPhase.CandidateShown);

            var showStatus = state.Phase == OnlineDuelPhase.InviteSent
                             || state.Phase == OnlineDuelPhase.Reserved
                             || state.Phase == OnlineDuelPhase.Consumed
                             || state.Phase == OnlineDuelPhase.EnterBattle;
            view.SetMatchStatusVisible(showStatus);
            if (showStatus) {
                view.SetMatchStatus(
                    FormatPlayerName(state.OpponentSessionId),
                    "",
                    string.IsNullOrWhiteSpace(state.OpponentScene) ? "Waiting" : state.OpponentScene);
            }
        }

        public void HideDuelDialog() {
            view.SetIncomingDuelVisible(false);
            view.SetCandidateDuelVisible(false);
        }

        static string FormatPlayerName(string sessionId) {
            if (string.IsNullOrWhiteSpace(sessionId)) {
                return "Opponent";
            }

            return sessionId.Length > 4 ? $"Player_{sessionId.Substring(0, 4)}" : sessionId;
        }

        public void Dispose() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            disposables.Dispose();
        }
    }
}
