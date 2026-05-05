using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class StageselectPresenter : IDisposable {
        const string LOG_PREFIX = "[StageSelectPresenter]";

        readonly StageselectScene view;
        readonly ISceneTransitionService transitionService;
        readonly IBattleSelectSetting selectSetting;
        readonly IMusicRegistry musicRegistry;
        readonly IAppBGMPlayer appBgmPlayer;
        readonly ILoadingOverlayService loadingOverlayService;
        readonly IOnlineDuelCoordinator onlineDuelCoordinator;
        readonly CompositeDisposable subscriptions = new();
        readonly Dictionary<string, MusicCardAddressableAssets> preloadedAssetsByMusicId = new();
        bool initialized;
        bool isPopupVisible;
        bool disposed;

        [Inject]
        public StageselectPresenter(
            StageselectScene view,
            ISceneTransitionService transitionService,
            IBattleSelectSetting selectSetting,
            IMusicRegistry musicRegistry,
            IAppBGMPlayer appBgmPlayer,
            ILoadingOverlayService loadingOverlayService,
            IOnlineDuelCoordinator onlineDuelCoordinator) {
            this.view = view;
            this.transitionService = transitionService;
            this.selectSetting = selectSetting;
            this.musicRegistry = musicRegistry;
            this.appBgmPlayer = appBgmPlayer;
            this.loadingOverlayService = loadingOverlayService;
            this.onlineDuelCoordinator = onlineDuelCoordinator;

            _ = InitializeAsync();
        }

        async Awaitable InitializeAsync() {
            if (initialized) {
                Debug.Log($"{LOG_PREFIX} Initialize skipped because already initialized");
                return;
            }

            Debug.Log($"{LOG_PREFIX} Initialize start");

            var musics = ResolveMusicList(musicRegistry);
            Debug.Log($"{LOG_PREFIX} Initialize music list resolved. count={musics.Count}");
            try {
                await PreloadMusicCardAssetsAsync(musics);
            }
            catch (Exception exception) {
                Debug.LogException(exception);
                ClearPreloadedAssets();
            }
            if (disposed) {
                return;
            }

            await EnterStageSelectAsync();
            if (disposed) {
                return;
            }

            foreach (var stageSelectButton in view.StageSelectButtons) {
                stageSelectButton.Initialize(musics, preloadedAssetsByMusicId);
                stageSelectButton.OnStageSelected.Subscribe(OnStageSelected).AddTo(subscriptions);
                stageSelectButton.OnMusicSelected.Subscribe(OnMusicSelected).AddTo(subscriptions);
                stageSelectButton.OnPreviewVisibilityChanged.Subscribe(OnPreviewVisibilityChanged).AddTo(subscriptions);
            }

            view.BackButton.OnBackPressed.Subscribe(_ => {
                Debug.Log($"{LOG_PREFIX} BackButton pressed. requesting start transition to {AppScene.Menu}");
                appBgmPlayer.Resume();
                var result = transitionService.RequestStartTransition(AppScene.Menu);
                Debug.Log($"{LOG_PREFIX} BackButton transition request result. isSuccess={result.IsSuccess}");
            }).AddTo(subscriptions);

            initialized = true;
            Debug.Log($"{LOG_PREFIX} Initialize completed");
        }

        async Task EnterStageSelectAsync() {
            Debug.Log($"{LOG_PREFIX} EnterStageSelectAsync requesting end transition. scene={AppScene.StageSelect}");
            var result = await transitionService.RequestEndTransitionAsync(AppScene.StageSelect);
            Debug.Log($"{LOG_PREFIX} EnterStageSelectAsync completed. isSuccess={result.IsSuccess}");
            if (result.IsSuccess) {
                await onlineDuelCoordinator.NotifySceneReadyAsync(AppScene.StageSelect);
            }
        }

        void OnStageSelected(Stage stage) {
            Debug.Log($"{LOG_PREFIX} OnStageSelected called. stage={stage}");
            selectSetting.SelectStage(stage);
        }

        void OnMusicSelected(MusicInfo musicInfo) {
            Debug.Log($"{LOG_PREFIX} OnMusicSelected called. musicId={musicInfo.Id}");
            selectSetting.SelectMusic(musicInfo.Id);
            appBgmPlayer.Resume();
            var result = transitionService.RequestStartTransition(AppScene.CharacterSelect);
            Debug.Log($"{LOG_PREFIX} OnMusicSelected transition request result. isSuccess={result.IsSuccess}, nextScene={AppScene.CharacterSelect}");
        }

        void OnPreviewVisibilityChanged(bool isVisible) {
            if (isPopupVisible == isVisible) {
                return;
            }

            isPopupVisible = isVisible;
            SyncPopupVisibility(isVisible);

            if (isVisible) {
                appBgmPlayer.Stop();
                return;
            }

            appBgmPlayer.Resume();
        }

        void SyncPopupVisibility(bool isVisible) {
            foreach (var stageSelectButton in view.StageSelectButtons) {
                stageSelectButton.SetPopupShown(isVisible);
            }
        }

        static IReadOnlyList<MusicInfo> ResolveMusicList(IMusicRegistry musicRegistry) {
            var getAllMethod = musicRegistry.GetType().GetMethod("GetAll");
            if (getAllMethod == null) {
                return Array.Empty<MusicInfo>();
            }

            var result = getAllMethod.Invoke(musicRegistry, null);
            return result as IReadOnlyList<MusicInfo> ?? Array.Empty<MusicInfo>();
        }

        async Awaitable PreloadMusicCardAssetsAsync(IReadOnlyList<MusicInfo> musics) {
            using var scope = loadingOverlayService.Begin();
            for (var i = 0; i < musics.Count; i++) {
                var music = musics[i];
                var previewClipAsset = await musicRegistry.LoadPreviewAudioClipAsync(music.Id);
                var spectrumAsset = await musicRegistry.LoadSpectrumDataAsync(music.Id);
                if (disposed) {
                    previewClipAsset.Dispose();
                    spectrumAsset.Dispose();
                    return;
                }

                if (preloadedAssetsByMusicId.TryGetValue(music.Id, out var existingAsset)) {
                    existingAsset.PreviewClipAsset?.Dispose();
                    existingAsset.SpectrumAsset?.Dispose();
                }

                preloadedAssetsByMusicId[music.Id] = new MusicCardAddressableAssets(previewClipAsset, spectrumAsset);
            }
        }

        public void Dispose() {
            disposed = true;
            subscriptions.Dispose();
            ClearPreloadedAssets();
        }

        void ClearPreloadedAssets() {
            foreach (var asset in preloadedAssetsByMusicId.Values) {
                asset.PreviewClipAsset?.Dispose();
                asset.SpectrumAsset?.Dispose();
            }

            preloadedAssetsByMusicId.Clear();
        }
    }
}
