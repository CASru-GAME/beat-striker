using System;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Alice {
    public interface IOnlineDuelCoordinator {
        Task NotifySceneReadyAsync(AppScene scene);
    }

    public class OnlineDuelCoordinator : IOnlineDuelCoordinator {
        const string LOG_PREFIX = "[OnlineDuelCoordinator]";
        const int PromptSkipTransitions = 5;

        readonly IOnlineDuelIdentity identity;
        readonly IOnlineDuelApiClient apiClient;
        readonly IOnlineDuelReservationStore reservationStore;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IAppOverlayPresenter appOverlayPresenter;

        bool isPromptInProgress;
        int candidateSkipRemaining;

        [Inject]
        public OnlineDuelCoordinator(
            IOnlineDuelIdentity identity,
            IOnlineDuelApiClient apiClient,
            IOnlineDuelReservationStore reservationStore,
            ISceneTransitionService sceneTransitionService,
            IAppNetworkSetting appNetworkSetting,
            IAppOverlayPresenter appOverlayPresenter) {
            this.identity = identity;
            this.apiClient = apiClient;
            this.reservationStore = reservationStore;
            this.sceneTransitionService = sceneTransitionService;
            this.appNetworkSetting = appNetworkSetting;
            this.appOverlayPresenter = appOverlayPresenter;
        }

        public async Task NotifySceneReadyAsync(AppScene scene) {
            if (IsBattleScene(scene) || isPromptInProgress || !appOverlayPresenter.IsOverlayVisible) {
                return;
            }

            if (reservationStore.HasReservation) {
                return;
            }

            isPromptInProgress = true;
            try {
                Debug.Log($"{LOG_PREFIX} Fetch prompt begin. scene={scene}, sessionId={identity.DuelSessionId}");
                await FetchAndShowPromptAsync(scene);
            }
            catch (Exception exception) {
                Debug.LogWarning($"{LOG_PREFIX} Prompt skipped for this transition. scene={scene}, reason={exception.Message}");
            }
            finally {
                isPromptInProgress = false;
            }
        }

        async Task FetchAndShowPromptAsync(AppScene scene) {
            var response = await apiClient.GetPromptsAsync(new DuelPromptRequest {
                duelSessionId = identity.DuelSessionId,
                scene = scene.ToString(),
                state = "Available",
            });
            Debug.Log($"{LOG_PREFIX} Fetch prompt completed. scene={scene}, hasReservation={response?.reservation != null}, hasIncoming={response?.incomingInvite != null}, hasCandidate={response?.candidate != null}");

            if (TryReserve(response?.reservation, out var reservationId)) {
                await HandleReservationAsync(scene, reservationId);
                return;
            }

            var suppressCandidate = candidateSkipRemaining > 0;
            if (suppressCandidate) {
                candidateSkipRemaining -= 1;
            }

            if (response?.incomingInvite != null && !string.IsNullOrWhiteSpace(response.incomingInvite.id)) {
                await HandleIncomingInviteAsync(scene, response.incomingInvite);
                return;
            }

            if (suppressCandidate) {
                return;
            }

            if (response?.candidate != null && !string.IsNullOrWhiteSpace(response.candidate.duelSessionId)) {
                await HandleCandidateAsync(response.candidate);
            }
        }

        async Task HandleIncomingInviteAsync(AppScene scene, DuelInviteDto invite) {
            var accepted = await appOverlayPresenter.ShowIncomingDuelAsync(invite);
            if (!accepted) {
                await apiClient.RejectInviteAsync(invite.id, new DuelInviteActionRequest {
                    duelSessionId = identity.DuelSessionId,
                });
                return;
            }

            var response = await apiClient.AcceptInviteAsync(invite.id, new DuelInviteActionRequest {
                duelSessionId = identity.DuelSessionId,
            });
            if (TryReserve(response?.reservation, out var reservationId)) {
                await HandleReservationAsync(scene, reservationId);
            }
        }

        async Task HandleCandidateAsync(DuelPresenceDto candidate) {
            var invite = await appOverlayPresenter.ShowDuelCandidateAsync(candidate);
            if (!invite) {
                candidateSkipRemaining = PromptSkipTransitions;
                return;
            }

            await apiClient.CreateInviteAsync(new DuelInviteCreateRequest {
                fromSessionId = identity.DuelSessionId,
                toSessionId = candidate.duelSessionId,
            });
        }

        async Task HandleReservationAsync(AppScene scene, string reservationId) {
            reservationStore.SetReservation(reservationId);
            appOverlayPresenter.HideDuelDialog();
            await TransitionToStageSelectIfNeededAsync(scene);
        }

        async Task TransitionToStageSelectIfNeededAsync(AppScene scene) {
            if (IsBattleScene(scene) || scene == AppScene.StageSelect) {
                return;
            }

            appNetworkSetting.SetIsOnline(true);
            var result = sceneTransitionService.RequestStartTransition(AppScene.StageSelect);
            if (!result.IsSuccess) {
                Debug.LogWarning($"{LOG_PREFIX} StageSelect transition rejected after reservation. currentScene={scene}");
            }

            await Task.CompletedTask;
        }

        static bool TryReserve(DuelReservationDto reservation, out string reservationId) {
            reservationId = reservation?.id ?? "";
            return !string.IsNullOrWhiteSpace(reservationId)
                && string.Equals(reservation.status, "reserved", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsBattleScene(AppScene scene) {
            return scene == AppScene.Live || scene == AppScene.Street;
        }
    }
}
