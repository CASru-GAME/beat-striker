using System;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public interface IAppOverlayPresenter {
        bool IsOverlayVisible { get; }
        void HideDuelDialog();
    }

    public class AppOverlayPresenter : IAppOverlayPresenter, IInitializable, IDisposable {
        const int CandidateDialogSkipTransitionBlockCount = 5;

        readonly AppOverlayView view;
        readonly IScreenRegistry screenRegistry;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IOnlineDuelFusionClient duelClient;
        readonly CompositeDisposable disposables = new();
        bool overlayEnabledForCurrentScreen;
        OnlineDuelPhase lastObservedPhase;
        string lastObservedInviteId = "";
        string lastHandledReservationId = "";
        int candidateDialogGateSceneSyncId;
        bool candidateDialogGateOpen;
        bool incomingDialogLatchedVisible;
        bool candidateDialogLatchedVisible;
        string latchedIncomingInviteId = "";
        string latchedCandidateSessionId = "";
        int candidateDialogSkipTransitionBlockRemaining;

        public bool IsOverlayVisible => overlayEnabledForCurrentScreen;

        [Inject]
        public AppOverlayPresenter(
            AppOverlayView view,
            IScreenRegistry screenRegistry,
            ISceneTransitionService sceneTransitionService,
            IAppNetworkSetting appNetworkSetting,
            IOnlineDuelFusionClient duelClient) {
            this.view = view;
            this.screenRegistry = screenRegistry;
            this.sceneTransitionService = sceneTransitionService;
            this.appNetworkSetting = appNetworkSetting;
            this.duelClient = duelClient;
        }

        public void Initialize() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyScreenRule(SceneManager.GetActiveScene().name);
            lastObservedPhase = duelClient.State.CurrentValue.Phase;
            lastObservedInviteId = duelClient.State.CurrentValue.InviteId;
            appNetworkSetting.IsOnline.Subscribe(_ => ApplyOnlineIndicatorFromNetwork()).AddTo(disposables);
            duelClient.State.Subscribe(OnDuelStateChanged).AddTo(disposables);
            sceneTransitionService.TransitioningChanged.Subscribe(OnTransitioningChanged).AddTo(disposables);
            sceneTransitionService.EndTransitionCompleted.Subscribe(OnEndTransitionCompleted).AddTo(disposables);
            Observable.EveryUpdate().Subscribe(_ => RefreshDynamicMatchStatus()).AddTo(disposables);
            view.IncomingDuelAccepted.Subscribe(_ => duelClient.AcceptInvite()).AddTo(disposables);
            view.IncomingDuelRejected.Subscribe(_ => duelClient.RejectInvite()).AddTo(disposables);
            view.CandidateDuelInvited.Subscribe(_ => duelClient.InviteCandidate()).AddTo(disposables);
            view.CandidateDuelSkipped.Subscribe(_ => SkipCandidateDuel()).AddTo(disposables);
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
                view.SetMatchStatusVisible(false);
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

        void OnDuelStateChanged(OnlineDuelUiState state) {
            ApplyDuelState(state);
            if (IsNewIncomingInvite(state)) {
                TryShowIncomingDuelDialog(state);
            }
            if (state.Phase == OnlineDuelPhase.CandidateShown && !candidateDialogLatchedVisible) {
                TryShowCandidateDuelDialog(state);
            }
            lastObservedPhase = state.Phase;
            lastObservedInviteId = state.InviteId;
        }

        void OnTransitioningChanged(bool isTransitioning) {
            if (isTransitioning) {
                HideDuelDialog();
            }
        }

        void OnEndTransitionCompleted(AppScene scene) {
            ApplyScreenRule(SceneManager.GetActiveScene().name);
            _ = NotifySceneReadyForOverlayAsync(scene);
            CountCandidateDialogSkippedTransition();
            OpenCandidateDialogGate();
            TryShowCandidateDuelDialog(duelClient.State.CurrentValue);
        }

        async System.Threading.Tasks.Task NotifySceneReadyForOverlayAsync(AppScene scene) {
            try {
                await duelClient.NotifySceneReadyAsync(scene);
            }
            catch (Exception exception) {
                Debug.LogWarning($"[AppOverlayPresenter] Scene ready sync failed. scene={scene}, message={exception.Message}");
            }
        }

        void ApplyDuelState(OnlineDuelUiState state) {
            if (!overlayEnabledForCurrentScreen) {
                HideDuelDialog();
                view.SetMatchStatusVisible(false);
                return;
            }

            ApplyLatchedDialogVisibility(state);

            ApplyMatchStatus(state);
            RequestStageSelectIfReserved(state);
        }

        void TryShowIncomingDuelDialog(OnlineDuelUiState state) {
            var visible = state.Phase == OnlineDuelPhase.IncomingInvite && CanShowDuelDialog();
            incomingDialogLatchedVisible = visible;
            latchedIncomingInviteId = visible ? state.InviteId : "";
            view.SetIncomingDuelVisible(visible);
        }

        void TryShowCandidateDuelDialog(OnlineDuelUiState state) {
            var visible = IsCandidateDialogAllowedForCurrentGate(state);
            candidateDialogLatchedVisible = visible;
            latchedCandidateSessionId = visible ? state.CandidateSessionId : "";
            if (visible) {
                candidateDialogGateOpen = false;
            }
            view.SetCandidateDuelVisible(visible);
        }

        void SkipCandidateDuel() {
            candidateDialogSkipTransitionBlockRemaining = CandidateDialogSkipTransitionBlockCount;
            candidateDialogGateOpen = false;
            candidateDialogLatchedVisible = false;
            latchedCandidateSessionId = "";
            view.SetCandidateDuelVisible(false);
            duelClient.SkipCandidate();
        }

        void CountCandidateDialogSkippedTransition() {
            if (candidateDialogSkipTransitionBlockRemaining > 0) {
                candidateDialogSkipTransitionBlockRemaining -= 1;
            }
        }

        void ApplyLatchedDialogVisibility(OnlineDuelUiState state) {
            var keepIncomingVisible = incomingDialogLatchedVisible
                                      && state.Phase == OnlineDuelPhase.IncomingInvite
                                      && state.InviteId == latchedIncomingInviteId
                                      && CanShowDuelDialog();
            view.SetIncomingDuelVisible(keepIncomingVisible);
            if (state.Phase != OnlineDuelPhase.IncomingInvite
                || (incomingDialogLatchedVisible && state.InviteId != latchedIncomingInviteId)) {
                incomingDialogLatchedVisible = false;
                latchedIncomingInviteId = "";
            }

            var keepCandidateVisible = candidateDialogLatchedVisible
                                       && state.Phase == OnlineDuelPhase.CandidateShown
                                       && state.CandidateSessionId == latchedCandidateSessionId
                                       && CanShowDuelDialog();
            view.SetCandidateDuelVisible(keepCandidateVisible);
            if (state.Phase != OnlineDuelPhase.CandidateShown
                || (candidateDialogLatchedVisible && state.CandidateSessionId != latchedCandidateSessionId)) {
                candidateDialogLatchedVisible = false;
                latchedCandidateSessionId = "";
            }
        }

        bool IsNewIncomingInvite(OnlineDuelUiState state) {
            return state.Phase == OnlineDuelPhase.IncomingInvite
                   && (lastObservedPhase != OnlineDuelPhase.IncomingInvite || state.InviteId != lastObservedInviteId);
        }

        void OpenCandidateDialogGate() {
            candidateDialogGateSceneSyncId = duelClient.LastSceneSyncId;
            candidateDialogGateOpen = CanShowDuelDialog() && candidateDialogGateSceneSyncId > 0;
        }

        bool IsCandidateDialogAllowedForCurrentGate(OnlineDuelUiState state) {
            return candidateDialogGateOpen
                   && candidateDialogSkipTransitionBlockRemaining <= 0
                   && state.Phase == OnlineDuelPhase.CandidateShown
                   && state.SceneSyncId == candidateDialogGateSceneSyncId
                   && CanShowDuelDialog();
        }

        bool CanShowDuelDialog() {
            return overlayEnabledForCurrentScreen && !sceneTransitionService.IsTransitioning;
        }

        void ApplyMatchStatus(OnlineDuelUiState state) {
            if (!overlayEnabledForCurrentScreen) {
                view.SetMatchStatusVisible(false);
                return;
            }

            var showStatus = ShouldShowMatchStatus(state);
            view.SetMatchStatusVisible(showStatus);
            if (!showStatus) {
                return;
            }

            view.SetMatchStatus(
                FormatPlayerName(ResolveOpponentSessionId(state)),
                FormatMatchTimeLimit(state, Time.realtimeSinceStartup),
                FormatOpponentStatus(state.OpponentStatus));
        }

        void RefreshDynamicMatchStatus() {
            var state = duelClient.State.CurrentValue;
            if (overlayEnabledForCurrentScreen && ShouldShowMatchStatus(state)) {
                ApplyMatchStatus(state);
            }
        }

        void RequestStageSelectIfReserved(OnlineDuelUiState state) {
            if (state.Phase != OnlineDuelPhase.Reserved) {
                return;
            }

            if (string.IsNullOrWhiteSpace(state.ReservationId) || state.ReservationId == lastHandledReservationId) {
                return;
            }

            var activeScene = SceneManager.GetActiveScene().name;
            if (!screenRegistry.TryGetBySceneName(activeScene, out var screenInfo)) {
                screenInfo = screenRegistry.Default;
            }

            if (screenInfo.Scene == AppScene.StageSelect
                || screenInfo.Scene == AppScene.CharacterSelect) {
                lastHandledReservationId = state.ReservationId;
                return;
            }

            var result = sceneTransitionService.RequestStartTransition(AppScene.StageSelect);
            if (result.IsSuccess) {
                lastHandledReservationId = state.ReservationId;
                Debug.Log($"[AppOverlayPresenter] Reserved duel accepted. Transitioning to {AppScene.StageSelect}. reservationId={state.ReservationId}");
            }
            else {
                Debug.LogWarning($"[AppOverlayPresenter] Reserved duel transition to {AppScene.StageSelect} rejected. reservationId={state.ReservationId}");
            }
        }

        public void HideDuelDialog() {
            view.SetIncomingDuelVisible(false);
            view.SetCandidateDuelVisible(false);
            candidateDialogGateOpen = false;
            candidateDialogGateSceneSyncId = 0;
            incomingDialogLatchedVisible = false;
            candidateDialogLatchedVisible = false;
            latchedIncomingInviteId = "";
            latchedCandidateSessionId = "";
        }

        static string FormatPlayerName(string sessionId) {
            if (string.IsNullOrWhiteSpace(sessionId)) {
                return "Opponent";
            }

            return sessionId.Length > 4 ? $"Player_{sessionId.Substring(0, 4)}" : sessionId;
        }

        static string ResolveOpponentSessionId(OnlineDuelUiState state) {
            if (!string.IsNullOrWhiteSpace(state.OpponentSessionId)) {
                return state.OpponentSessionId;
            }
            if (!string.IsNullOrWhiteSpace(state.CandidateSessionId)) {
                return state.CandidateSessionId;
            }
            if (!string.IsNullOrWhiteSpace(state.InviteFromSessionId)) {
                return state.InviteFromSessionId;
            }
            if (!string.IsNullOrWhiteSpace(state.InviteToSessionId)) {
                return state.InviteToSessionId;
            }

            return "";
        }

        public static bool ShouldShowMatchStatus(OnlineDuelUiState state) {
            return IsReservedDuelPhase(state.Phase) || IsActiveMatchmaking(state);
        }

        static bool IsReservedDuelPhase(OnlineDuelPhase phase) {
            return phase == OnlineDuelPhase.Reserved
                   || phase == OnlineDuelPhase.Consumed
                   || phase == OnlineDuelPhase.EnterBattle;
        }

        static bool IsActiveMatchmaking(OnlineDuelUiState state) {
            return state.Phase == OnlineDuelPhase.Matching
                   && state.MatchDeadlineRealtime > 0f;
        }

        public static string FormatMatchTimeLimit(OnlineDuelUiState state, float nowRealtime) {
            if (state.Phase != OnlineDuelPhase.Matching || state.MatchDeadlineRealtime <= 0f) {
                return "";
            }

            return Mathf.Max(0, Mathf.CeilToInt(state.MatchDeadlineRealtime - nowRealtime)).ToString();
        }

        public static string FormatOpponentStatus(OnlineDuelPlayerStatus status) {
            return status switch {
                OnlineDuelPlayerStatus.StageSelecting => "ステージ選択中",
                OnlineDuelPlayerStatus.CharacterSelecting => "キャラ選択中",
                OnlineDuelPlayerStatus.Waiting => "待機中",
                _ => "ステージ選択中",
            };
        }

        public void Dispose() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            disposables.Dispose();
        }
    }
}
