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
        readonly AppOverlayView view;
        readonly IScreenRegistry screenRegistry;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IMatchingModel matchingModel;
        readonly IMatchingDuelOperations duelOperations;
        readonly IAppNetworkSetting networkSetting;
        readonly CompositeDisposable disposables = new();
        bool overlayEnabledForCurrentScreen;
        float lastCandidateDialogSkippedAt = float.NegativeInfinity;

        public bool IsOverlayVisible => overlayEnabledForCurrentScreen;

        [Inject]
        public AppOverlayPresenter(
            AppOverlayView view,
            IScreenRegistry screenRegistry,
            ISceneTransitionService sceneTransitionService,
            IMatchingModel matchingModel,
            IMatchingDuelOperations duelOperations,
            IAppNetworkSetting networkSetting) {
            this.view = view;
            this.screenRegistry = screenRegistry;
            this.sceneTransitionService = sceneTransitionService;
            this.matchingModel = matchingModel;
            this.duelOperations = duelOperations;
            this.networkSetting = networkSetting;
        }

        public void Initialize() {
            Debug.Log($"[AppOverlayPresenter] Initialize begin. activeScene={SceneManager.GetActiveScene().name}, overlayEnabledForCurrentScreen={overlayEnabledForCurrentScreen}");
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyScreenRule(SceneManager.GetActiveScene().name);
            matchingModel.IsEstablished.Subscribe(_ => ApplyOnlineIndicatorFromModel()).AddTo(disposables);
            matchingModel.State.Subscribe(OnMatchingStateChanged).AddTo(disposables);
            sceneTransitionService.EndTransitionCompleted.Subscribe(OnEndTransitionCompleted).AddTo(disposables);
            Observable.EveryUpdate().Subscribe(_ => RefreshDynamicMatchStatus()).AddTo(disposables);
            view.IncomingDuelAccepted.Subscribe(_ => AcceptIncomingDuel()).AddTo(disposables);
            view.IncomingDuelRejected.Subscribe(_ => RejectIncomingDuel()).AddTo(disposables);
            view.CandidateDuelInvited.Subscribe(_ => InviteCandidateDuel()).AddTo(disposables);
            view.CandidateDuelSkipped.Subscribe(_ => SkipCandidateDuel()).AddTo(disposables);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            ApplyScreenRule(scene.name);
        }

        void ApplyScreenRule(string sceneName) {
            if (!screenRegistry.TryGetBySceneName(sceneName, out var screenInfo)) {
                screenInfo = screenRegistry.Default;
            }

            var wasOverlayEnabled = overlayEnabledForCurrentScreen;
            overlayEnabledForCurrentScreen = screenInfo.ShowAppOverlay;
            view.SetOverlayVisible(overlayEnabledForCurrentScreen);
            if (!overlayEnabledForCurrentScreen) {
                HideDuelDialog(cancelActiveDuelUi: true);
                view.SetOnlineIndicatorVisible(false);
                view.SetMatchStatusVisible(false);
                if (wasOverlayEnabled) {
                    _ = duelOperations.NotifySceneReadyAsync(screenInfo.Scene, false);
                }
                return;
            }
            ApplyOnlineIndicatorFromModel();
            ApplyDuelState(matchingModel.State.CurrentValue);
            ApplyMatchStatusFromModel(matchingModel.State.CurrentValue);
        }

        void ApplyOnlineIndicatorFromModel() {
            if (!overlayEnabledForCurrentScreen) {
                return;
            }

            view.SetOnlineIndicatorVisible(matchingModel.IsEstablished.CurrentValue);
        }

        void OnMatchingStateChanged(MatchingState state) {
            ApplyDuelState(state);
            ApplyMatchStatusFromModel(state);
        }

        void OnEndTransitionCompleted(AppScene scene) {
            _ = OnEndTransitionCompletedAsync(scene);
        }

        async System.Threading.Tasks.Task OnEndTransitionCompletedAsync(AppScene scene) {
            ApplyScreenRule(SceneManager.GetActiveScene().name);
            await duelOperations.NotifySceneReadyAsync(scene, overlayEnabledForCurrentScreen);
            ApplyDuelState(matchingModel.State.CurrentValue);
        }

        void ApplyDuelState(MatchingState state) {
            if (!overlayEnabledForCurrentScreen) {
                HideDuelDialog(cancelActiveDuelUi: true);
                return;
            }

            var ctx = new OverlayCtx(overlayEnabledForCurrentScreen, IsCandidateDialogSkipCooldownActive());
            view.SetIncomingDuelVisible(ShouldShowIncoming(state, ctx));
            view.SetCandidateDuelVisible(ShouldShowCandidate(state, ctx));
        }

        void SkipCandidateDuel() {
            lastCandidateDialogSkippedAt = Time.realtimeSinceStartup;
            duelOperations.SkipCandidate();
            ApplyDuelState(matchingModel.State.CurrentValue);
        }

        void InviteCandidateDuel() {
            duelOperations.InviteCandidate();
        }

        void AcceptIncomingDuel() {
            duelOperations.AcceptInvite();
        }

        void RejectIncomingDuel() {
            duelOperations.RejectInvite();
        }

        bool IsCandidateDialogSkipCooldownActive() {
            return Time.realtimeSinceStartup - lastCandidateDialogSkippedAt < networkSetting.DuelInviteSkipCooldownSeconds;
        }

        void ApplyMatchStatusFromModel(MatchingState state) {
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
                FormatMatchingPhase(state.OpponentPhase));
        }

        void RefreshDynamicMatchStatus() {
            var state = matchingModel.State.CurrentValue;
            if (overlayEnabledForCurrentScreen && ShouldShowMatchStatus(state)) {
                ApplyMatchStatusFromModel(state);
            }
        }

        public void HideDuelDialog() {
            HideDuelDialog(cancelActiveDuelUi: true);
        }

        void HideDuelDialog(bool cancelActiveDuelUi) {
            if (cancelActiveDuelUi) {
                CancelActiveDuelUi();
            }

            view.SetIncomingDuelVisible(false);
            view.SetCandidateDuelVisible(false);
        }

        void CancelActiveDuelUi() {
            var state = matchingModel.State.CurrentValue;
            if (state.HasIncomingInvite) {
                duelOperations.RejectInvite();
                return;
            }

            if (state.HasInviteCandidate) {
                duelOperations.SkipCandidate();
            }
        }

        static string FormatPlayerName(string sessionId) {
            if (string.IsNullOrWhiteSpace(sessionId)) {
                return "Opponent";
            }

            return sessionId.Length > 4 ? $"Player_{sessionId.Substring(0, 4)}" : sessionId;
        }

        static string ResolveOpponentSessionId(MatchingState state) {
            return state.OpponentSessionId;
        }

        public static bool ShouldShowMatchStatus(MatchingState state) {
            return state.Phase == MatchingPhase.StageSelecting
                   || state.Phase == MatchingPhase.CharacterSelecting
                   || state.Phase == MatchingPhase.Waiting
                   || state.Phase == MatchingPhase.InBattle;
        }

        public static string FormatMatchTimeLimit(MatchingState state, float nowRealtime) {
            if ((state.Phase != MatchingPhase.StageSelecting && state.Phase != MatchingPhase.CharacterSelecting)
                || state.MatchDeadlineRealtime <= 0f) {
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

        public static string FormatMatchingPhase(MatchingPhase phase) {
            return phase switch {
                MatchingPhase.InvitingOrGuidance => "宣戦布告中",
                MatchingPhase.StageSelecting => "ステージ選択中",
                MatchingPhase.CharacterSelecting => "キャラ選択中",
                MatchingPhase.Waiting => "待機中",
                MatchingPhase.InBattle => "バトル中",
                _ => "ステージ選択中",
            };
        }

        static bool ShouldShowIncoming(MatchingState state, OverlayCtx ctx) {
            return state.HasIncomingInvite && ctx.OverlayEnabled;
        }

        static bool ShouldShowCandidate(MatchingState state, OverlayCtx ctx) {
            return state.IsCandidateGuidance
                   && !state.HasIncomingInvite
                   && !state.HasReservation
                   && ctx.OverlayEnabled
                   && !ctx.SkipCooldownActive;
        }

        record OverlayCtx(bool OverlayEnabled, bool SkipCooldownActive);

        public void Dispose() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            disposables.Dispose();
        }
    }
}
