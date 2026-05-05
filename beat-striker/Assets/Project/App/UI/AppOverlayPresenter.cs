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

        readonly CompositeDisposable disposables = new();
        TaskCompletionSource<bool> duelDialogCompletion;
        bool overlayEnabledForCurrentScreen;
        bool showingDuelDialog;

        public bool IsOverlayVisible => overlayEnabledForCurrentScreen;

        [Inject]
        public AppOverlayPresenter(
            AppOverlayView view,
            IScreenRegistry screenRegistry,
            IAppNetworkSetting appNetworkSetting) {
            this.view = view;
            this.screenRegistry = screenRegistry;
            this.appNetworkSetting = appNetworkSetting;
        }

        public void Initialize() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyScreenRule(SceneManager.GetActiveScene().name);
            appNetworkSetting.IsOnline.Subscribe(_ => ApplyOnlineIndicatorFromNetwork()).AddTo(disposables);
            view.IncomingDuelAccepted.Subscribe(_ => CompleteDuelDialog(true)).AddTo(disposables);
            view.IncomingDuelRejected.Subscribe(_ => CompleteDuelDialog(false)).AddTo(disposables);
            view.CandidateDuelInvited.Subscribe(_ => CompleteDuelDialog(true)).AddTo(disposables);
            view.CandidateDuelSkipped.Subscribe(_ => CompleteDuelDialog(false)).AddTo(disposables);
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
