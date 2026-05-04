using System;
using R3;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class AppOverlayPresenter : IInitializable, IDisposable {
        readonly AppOverlayView view;
        readonly IScreenRegistry screenRegistry;
        readonly IAppNetworkSetting appNetworkSetting;

        readonly CompositeDisposable disposables = new();
        bool overlayEnabledForCurrentScreen;

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
            ApplyOnlineIndicatorFromNetwork();
        }

        void ApplyOnlineIndicatorFromNetwork() {
            if (!overlayEnabledForCurrentScreen) {
                return;
            }

            view.SetOnlineIndicatorVisible(appNetworkSetting.IsOnline.CurrentValue);
        }

        public void Dispose() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            disposables.Dispose();
        }
    }
}
