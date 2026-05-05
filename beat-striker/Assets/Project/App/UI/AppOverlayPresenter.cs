using System;
using System.Threading.Tasks;
using R3;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public interface IAppOverlayPresenter {
        bool IsOverlayVisible { get; }
        Task<bool> ShowIncomingDuelAsync(DuelInviteDto invite);
        Task<bool> ShowDuelCandidateAsync(DuelPresenceDto candidate);
        void HideDuelDialog();
    }

    public class AppOverlayPresenter : IAppOverlayPresenter, IInitializable, IDisposable {
        readonly AppOverlayView view;
        readonly IScreenRegistry screenRegistry;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IOnlineDuelReservationStore reservationStore;
        readonly IOnlineDuelIdentity identity;
        readonly IOnlineDuelApiClient apiClient;

        readonly CompositeDisposable disposables = new();
        TaskCompletionSource<bool> duelDialogCompletion;
        bool overlayEnabledForCurrentScreen;
        bool showingDuelDialog;
        IDisposable pollingDisposable;

        public bool IsOverlayVisible => overlayEnabledForCurrentScreen;

        [Inject]
        public AppOverlayPresenter(
            AppOverlayView view,
            IScreenRegistry screenRegistry,
            IAppNetworkSetting appNetworkSetting,
            IOnlineDuelReservationStore reservationStore,
            IOnlineDuelIdentity identity,
            IOnlineDuelApiClient apiClient) {
            this.view = view;
            this.screenRegistry = screenRegistry;
            this.appNetworkSetting = appNetworkSetting;
            this.reservationStore = reservationStore;
            this.identity = identity;
            this.apiClient = apiClient;
        }

        public void Initialize() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyScreenRule(SceneManager.GetActiveScene().name);
            appNetworkSetting.IsOnline.Subscribe(_ => ApplyOnlineIndicatorFromNetwork()).AddTo(disposables);
            view.IncomingDuelAccepted.Subscribe(_ => CompleteDuelDialog(true)).AddTo(disposables);
            view.IncomingDuelRejected.Subscribe(_ => CompleteDuelDialog(false)).AddTo(disposables);
            view.CandidateDuelInvited.Subscribe(_ => CompleteDuelDialog(true)).AddTo(disposables);
            view.CandidateDuelSkipped.Subscribe(_ => CompleteDuelDialog(false)).AddTo(disposables);
            Observable.Interval(TimeSpan.FromSeconds(3)).Subscribe(_ => PollMatchStatusAsync()).AddTo(disposables);
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
        }

        void ApplyOnlineIndicatorFromNetwork() {
            if (!overlayEnabledForCurrentScreen) {
                return;
            }

            view.SetOnlineIndicatorVisible(appNetworkSetting.IsOnline.CurrentValue);
        }

        async void PollMatchStatusAsync() {
            if (!overlayEnabledForCurrentScreen || !appNetworkSetting.IsOnline.CurrentValue || !reservationStore.HasReservation) {
                view.SetMatchStatusVisible(false);
                return;
            }

            try {
                var response = await apiClient.GetPromptsAsync(new DuelPromptRequest {
                    duelSessionId = identity.DuelSessionId,
                    scene = SceneManager.GetActiveScene().name,
                    state = "Matched",
                });

                if (response?.reservation != null) {
                    var opponentSessionId = "Opponent";
                    if (response.reservation.playerSessionIds != null) {
                        foreach (var id in response.reservation.playerSessionIds) {
                            if (id != identity.DuelSessionId) {
                                opponentSessionId = id;
                                break;
                            }
                        }
                    }

                    string timeLimitStr = "";
                    if (!string.IsNullOrEmpty(response.reservation.expiresAt)) {
                        if (DateTime.TryParse(response.reservation.expiresAt, out var expiresAt)) {
                            var remaining = expiresAt - DateTime.UtcNow;
                            timeLimitStr = remaining.TotalSeconds > 0 ? $"{remaining.TotalSeconds:F0}s" : "0s";
                        }
                    }

                    string opponentStatus = "Waiting";
                    if (response.opponentPresence != null) {
                        opponentStatus = string.IsNullOrEmpty(response.opponentPresence.scene) ? "Waiting" : response.opponentPresence.scene;
                    }

                    // Format the opponent name nicely (e.g. Player_1234)
                    string formattedOpponentName = opponentSessionId.Length > 4 ? $"Player_{opponentSessionId.Substring(0, 4)}" : opponentSessionId;
                    
                    view.SetMatchStatus(formattedOpponentName, timeLimitStr, opponentStatus);
                    view.SetMatchStatusVisible(true);
                } else {
                    view.SetMatchStatusVisible(false);
                }
            } catch (Exception ex) {
                UnityEngine.Debug.LogWarning($"[AppOverlayPresenter] PollMatchStatus failed: {ex.Message}");
            }
        }

        public async Task<bool> ShowIncomingDuelAsync(DuelInviteDto invite) {
            HideDuelDialog();
            showingDuelDialog = true;
            duelDialogCompletion = new TaskCompletionSource<bool>();
            view.SetCandidateDuelVisible(false);
            view.SetIncomingDuelVisible(true);
            var accepted = await duelDialogCompletion.Task;
            HideDuelDialog();
            return accepted;
        }

        public async Task<bool> ShowDuelCandidateAsync(DuelPresenceDto candidate) {
            HideDuelDialog();
            showingDuelDialog = true;
            duelDialogCompletion = new TaskCompletionSource<bool>();
            view.SetIncomingDuelVisible(false);
            view.SetCandidateDuelVisible(true);
            var invited = await duelDialogCompletion.Task;
            HideDuelDialog();
            return invited;
        }

        public void HideDuelDialog() {
            view.SetIncomingDuelVisible(false);
            view.SetCandidateDuelVisible(false);
            showingDuelDialog = false;
            if (duelDialogCompletion != null && !duelDialogCompletion.Task.IsCompleted) {
                duelDialogCompletion.TrySetResult(false);
            }
        }

        void CompleteDuelDialog(bool result) {
            if (!showingDuelDialog) {
                return;
            }

            duelDialogCompletion?.TrySetResult(result);
        }

        public void Dispose() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            disposables.Dispose();
        }
    }
}
