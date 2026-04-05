using System;
using System.Collections.Generic;
using R3;

namespace Alice {
    public class StageselectPresenter : IDisposable {
        readonly StageselectScene view;
        readonly ISceneTransitionService transitionService;
        readonly IBattleSelectSetting selectSetting;
        readonly IMusicRegistry musicRegistry;
        readonly IAppBGMPlayer appBgmPlayer;
        readonly CompositeDisposable subscriptions = new();
        bool initialized;

        public StageselectPresenter(
            StageselectScene view,
            ISceneTransitionService transitionService,
            IBattleSelectSetting selectSetting,
            IMusicRegistry musicRegistry,
            IAppBGMPlayer appBgmPlayer) {
            this.view = view;
            this.transitionService = transitionService;
            this.selectSetting = selectSetting;
            this.musicRegistry = musicRegistry;
            this.appBgmPlayer = appBgmPlayer;

            Initialize();
        }

        void Initialize() {
            if (initialized) {
                return;
            }

            var musics = ResolveMusicList(musicRegistry);
            foreach (var stageSelectButton in view.StageSelectButtons) {
                stageSelectButton.Initialize(musics);
                stageSelectButton.OnStageSelected.Subscribe(OnStageSelected).AddTo(subscriptions);
                stageSelectButton.OnMusicSelected.Subscribe(OnMusicSelected).AddTo(subscriptions);
                stageSelectButton.OnPreviewVisibilityChanged.Subscribe(OnPreviewVisibilityChanged).AddTo(subscriptions);
            }

            view.BackButton.OnBackPressed.Subscribe(_ => {
                appBgmPlayer.Resume();
                transitionService.RequestStartTransition(AppScene.Title);
            }).AddTo(subscriptions);

            _ = transitionService.RequestEndTransitionAsync(AppScene.StageSelect);
            initialized = true;
        }

        void OnStageSelected(Stage stage) {
            selectSetting.SelectStage(stage);
        }

        void OnMusicSelected(MusicInfo musicInfo) {
            selectSetting.SelectMusic(musicInfo.Id);
            appBgmPlayer.Resume();
            transitionService.RequestStartTransition(AppScene.CharacterSelect);
        }

        void OnPreviewVisibilityChanged(bool isVisible) {
            if (isVisible) {
                appBgmPlayer.Stop();
                return;
            }

            appBgmPlayer.Resume();
        }

        static IReadOnlyList<MusicInfo> ResolveMusicList(IMusicRegistry musicRegistry) {
            return (IReadOnlyList<MusicInfo>)musicRegistry.GetType().GetMethod("GetAll").Invoke(musicRegistry, null);
        }

        public void Dispose() {
            subscriptions.Dispose();
        }
    }
}
