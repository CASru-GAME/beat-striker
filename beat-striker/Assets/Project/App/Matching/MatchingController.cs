using System;
using System.Threading;
using System.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public interface IMatchingDuelOperations {
        int LastSceneSyncId { get; }
        Task NotifySceneReadyAsync(AppScene scene, bool appOverlayEnabled);
        Task NotifyPlayerStatusAsync(OnlineDuelPlayerStatus status);
        void InviteCandidate();
        void SkipCandidate();
        void AcceptInvite();
        void RejectInvite();
        void CancelInvite();
        void ConsumeReservation();
        void CancelMatchmaking();
        Task<OnlineMatchResult> MatchAsync(OnlineMatchRequest request);
    }

    public class MatchingController : IMatchingDuelOperations, IInitializable, IDisposable {
        const string LOG_PREFIX = "[MatchingController]";

        readonly IMutableMatchingModel matchingModel;
        readonly IOnlineDuelFusionClient duelClient;
        readonly IOnlineSessionBootstrap sessionBootstrap;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IScreenRegistry screenRegistry;
        readonly IAppNetworkSetting networkSetting;
        readonly CompositeDisposable disposables = new();
        readonly SemaphoreSlim cleanupGate = new(1, 1);
        string lastHandledReservationId = "";
        AppScene currentScene = AppScene.Title;
        OnlineDuelPlayerStatus localPlayerStatus = OnlineDuelPlayerStatus.StageSelecting;

        [Inject]
        public MatchingController(
            IMutableMatchingModel matchingModel,
            IOnlineDuelFusionClient duelClient,
            IOnlineSessionBootstrap sessionBootstrap,
            ISceneTransitionService sceneTransitionService,
            IScreenRegistry screenRegistry,
            IAppNetworkSetting networkSetting) {
            this.matchingModel = matchingModel;
            this.duelClient = duelClient;
            this.sessionBootstrap = sessionBootstrap;
            this.sceneTransitionService = sceneTransitionService;
            this.screenRegistry = screenRegistry;
            this.networkSetting = networkSetting;
        }

        public void Initialize() {
            duelClient.State.Subscribe(ApplyDuelState).AddTo(disposables);
            sceneTransitionService.EndTransitionCompleted.Subscribe(OnEndTransitionCompleted).AddTo(disposables);
            Observable.EveryUpdate().Subscribe(_ => Tick()).AddTo(disposables);

            ApplySceneRule(SceneManager.GetActiveScene().name, afterTransitionCompleted: false);
            ApplyDuelState(duelClient.State.CurrentValue);
        }

        void ApplyDuelState(OnlineDuelUiState state) {
            var previous = matchingModel.State.CurrentValue;
            var phase = ResolvePhase(state);
            var next = new MatchingState(
                phase,
                state.LocalSessionId,
                state.ReservationId,
                state.OpponentSessionId,
                ResolveOpponentPhase(state),
                state.OpponentStatus,
                ResolveSelectionDeadline(previous, state.ReservationId, phase),
                duelClient.LastSceneSyncId,
                state.UiMode == OnlineDuelUiMode.IncomingInvite ? state.InviteId : "",
                state.UiMode == OnlineDuelUiMode.IncomingInvite ? state.InviteFromSessionId : "",
                state.UiMode == OnlineDuelUiMode.Candidate ? state.CandidateSessionId : "",
                state.UiMode == OnlineDuelUiMode.Candidate,
                state.Message);
            matchingModel.SetState(next);

            if (next.Phase == MatchingPhase.Error) {
                _ = ClearMatchingAsync(next.Message);
                return;
            }

            if (next.IsPreBattleMatched && state.ReservationId != lastHandledReservationId && !string.IsNullOrWhiteSpace(state.ReservationId)) {
                ForceStageSelect(state.ReservationId);
            }

            if (previous.IsPreBattleMatched && !next.HasReservation && next.Phase == MatchingPhase.Idle) {
                _ = ClearMatchingAndReturnTitleAsync(next.Message);
                return;
            }

            if (previous.Phase == MatchingPhase.Error && !next.IsActive) {
                matchingModel.Clear(next.Message);
            }
        }

        void Tick() {
            var state = matchingModel.State.CurrentValue;
            if (state.MatchDeadlineRealtime <= 0f || !state.IsActive) {
                return;
            }

            if (Time.realtimeSinceStartup >= state.MatchDeadlineRealtime) {
                _ = ClearMatchingAndReturnTitleAsync("Selection time limit exceeded.");
            }
        }

        void OnEndTransitionCompleted(AppScene scene) {
            currentScene = scene;
            localPlayerStatus = ResolveInitialPlayerStatus(scene);
            ApplySceneRule(SceneManager.GetActiveScene().name, afterTransitionCompleted: true);
            ApplyDuelState(duelClient.State.CurrentValue);
        }

        void ApplySceneRule(string sceneName, bool afterTransitionCompleted) {
            var scene = ResolveScene(sceneName);
            currentScene = scene;
            if (IsAllowedScene(scene)) {
                return;
            }

            if (matchingModel.State.CurrentValue.IsPreBattleMatched) {
                _ = ClearMatchingAndReturnTitleAsync($"Scene not allowed: {scene}");
                return;
            }

            if (afterTransitionCompleted) {
                Debug.Log($"{LOG_PREFIX} Scene is not allowed for matching state, but online runner is kept alive. scene={scene}");
            }
        }

        AppScene ResolveScene(string sceneName) {
            if (!screenRegistry.TryGetBySceneName(sceneName, out var screenInfo)) {
                screenInfo = screenRegistry.Default;
            }

            return screenInfo.Scene;
        }

        static bool IsAllowedScene(AppScene scene) {
            return scene == AppScene.StageSelect
                || scene == AppScene.CharacterSelect
                || scene == AppScene.Live
                || scene == AppScene.Street;
        }

        void ForceStageSelect(string reservationId) {
            var result = sceneTransitionService.RequestStartTransition(AppScene.StageSelect);
            if (result.IsSuccess) {
                lastHandledReservationId = reservationId;
                Debug.Log($"{LOG_PREFIX} Reserved duel established. Transitioning to {AppScene.StageSelect}. reservationId={reservationId}");
                return;
            }

            Debug.LogWarning($"{LOG_PREFIX} Reserved duel transition to {AppScene.StageSelect} rejected. reservationId={reservationId}");
        }

        void ForceTitle(string reason) {
            if (currentScene == AppScene.Title) {
                return;
            }

            var result = sceneTransitionService.RequestStartTransition(AppScene.Title);
            if (result.IsSuccess) {
                Debug.Log($"{LOG_PREFIX} Pre-battle duel cleared. Transitioning to {AppScene.Title}. reason={reason}");
                return;
            }

            Debug.LogWarning($"{LOG_PREFIX} Pre-battle duel transition to {AppScene.Title} rejected. reason={reason}");
        }

        async Task ClearMatchingAsync(string reason) {
            await cleanupGate.WaitAsync();
            try {
                matchingModel.Clear(reason ?? "");
                sessionBootstrap.CancelMatchmaking();
            }
            finally {
                cleanupGate.Release();
            }
        }

        async Task ClearMatchingAndReturnTitleAsync(string reason) {
            await ClearMatchingAsync(reason);
            ForceTitle(reason ?? "");
        }

        MatchingPhase ResolvePhase(OnlineDuelUiState state) {
            return state.UiMode switch {
                OnlineDuelUiMode.Candidate => MatchingPhase.InvitingOrGuidance,
                OnlineDuelUiMode.IncomingInvite => MatchingPhase.InvitingOrGuidance,
                OnlineDuelUiMode.InviteSent => MatchingPhase.InvitingOrGuidance,
                OnlineDuelUiMode.Matched => ResolveMatchedPhase(),
                OnlineDuelUiMode.EnterBattle => MatchingPhase.InBattle,
                OnlineDuelUiMode.Error => MatchingPhase.Error,
                _ => MatchingPhase.Idle,
            };
        }

        MatchingPhase ResolveMatchedPhase() {
            if (IsBattleScene(currentScene)) {
                return MatchingPhase.InBattle;
            }

            return currentScene switch {
                AppScene.CharacterSelect => localPlayerStatus == OnlineDuelPlayerStatus.Waiting
                    ? MatchingPhase.Waiting
                    : MatchingPhase.CharacterSelecting,
                _ => MatchingPhase.StageSelecting,
            };
        }

        static MatchingPhase ResolveOpponentPhase(OnlineDuelUiState state) {
            return state.UiMode switch {
                OnlineDuelUiMode.IncomingInvite => MatchingPhase.InvitingOrGuidance,
                OnlineDuelUiMode.InviteSent => MatchingPhase.InvitingOrGuidance,
                OnlineDuelUiMode.Matched => ResolveOpponentMatchedPhase(state.OpponentScene, state.OpponentStatus),
                OnlineDuelUiMode.EnterBattle => MatchingPhase.InBattle,
                OnlineDuelUiMode.Error => MatchingPhase.Error,
                _ => MatchingPhase.Idle,
            };
        }

        static MatchingPhase ResolveOpponentMatchedPhase(string opponentScene, OnlineDuelPlayerStatus opponentStatus) {
            if (IsBattleSceneName(opponentScene)) {
                return MatchingPhase.InBattle;
            }

            if (opponentStatus == OnlineDuelPlayerStatus.Waiting) {
                return MatchingPhase.Waiting;
            }

            return opponentScene switch {
                nameof(AppScene.CharacterSelect) => MatchingPhase.CharacterSelecting,
                _ => MatchingPhase.StageSelecting,
            };
        }

        float ResolveSelectionDeadline(MatchingState previous, string reservationId, MatchingPhase phase) {
            if (!IsSelectionTimeLimitedPhase(phase)) {
                return 0f;
            }

            if (previous.ReservationId == reservationId
                && IsSelectionTimeLimitedPhase(previous.Phase)
                && previous.MatchDeadlineRealtime > Time.realtimeSinceStartup) {
                return previous.MatchDeadlineRealtime;
            }

            return Time.realtimeSinceStartup + networkSetting.SelectionTimeLimitSeconds;
        }

        static bool IsSelectionTimeLimitedPhase(MatchingPhase phase) {
            return phase == MatchingPhase.StageSelecting || phase == MatchingPhase.CharacterSelecting;
        }

        static bool IsBattleScene(AppScene scene) {
            return scene == AppScene.Live || scene == AppScene.Street;
        }

        static bool IsBattleSceneName(string scene) {
            return scene == nameof(AppScene.Live) || scene == nameof(AppScene.Street);
        }

        static OnlineDuelPlayerStatus ResolveInitialPlayerStatus(AppScene scene) {
            return scene switch {
                AppScene.CharacterSelect => OnlineDuelPlayerStatus.CharacterSelecting,
                _ => OnlineDuelPlayerStatus.StageSelecting,
            };
        }

        public int LastSceneSyncId => duelClient.LastSceneSyncId;

        public async Task NotifySceneReadyAsync(AppScene scene, bool appOverlayEnabled) {
            try {
                currentScene = scene;
                localPlayerStatus = ResolveInitialPlayerStatus(scene);
                await duelClient.NotifySceneReadyAsync(scene, appOverlayEnabled);
                ApplyDuelState(duelClient.State.CurrentValue);
            }
            catch (Exception exception) {
                Debug.LogWarning($"{LOG_PREFIX} Scene ready notification failed. scene={scene}, message={exception.Message}");
            }
        }

        public async Task NotifyPlayerStatusAsync(OnlineDuelPlayerStatus status) {
            try {
                localPlayerStatus = status;
                await duelClient.NotifyPlayerStatusAsync(status);
                ApplyDuelState(duelClient.State.CurrentValue);
            }
            catch (Exception exception) {
                Debug.LogWarning($"{LOG_PREFIX} Player status notification failed. status={status}, message={exception.Message}");
            }
        }

        public void InviteCandidate() {
            duelClient.InviteCandidate();
        }

        public void SkipCandidate() {
            duelClient.SkipCandidate();
        }

        public void AcceptInvite() {
            duelClient.AcceptInvite();
        }

        public void RejectInvite() {
            duelClient.RejectInvite();
        }

        public void CancelInvite() {
            duelClient.CancelInvite();
        }

        public void ConsumeReservation() {
            duelClient.ConsumeReservation();
        }

        public void CancelMatchmaking() {
            sessionBootstrap.CancelMatchmaking();
        }

        public Task<OnlineMatchResult> MatchAsync(OnlineMatchRequest request) {
            return sessionBootstrap.MatchAsync(request);
        }

        public void Dispose() {
            disposables.Dispose();
            cleanupGate.Dispose();
        }
    }
}
