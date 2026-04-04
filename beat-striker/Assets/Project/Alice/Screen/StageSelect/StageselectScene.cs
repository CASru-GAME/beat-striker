using Alice;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;

public class StageselectScene : MonoBehaviour
{
    [SerializeField] Backbutton backButton;
    [SerializeField] Stageselectbutton[] stageSelectButtons;

    ISceneTransitionService transitionService;
    IBattleSelectSetting selectSetting;
    IMusicRegistry musicRegistry;
    IAppBGMPlayer appBgmPlayer;
    readonly CompositeDisposable subscriptions = new();
    bool initialized;

    [Inject]
    public void Construct(
        ISceneTransitionService transitionService,
        IBattleSelectSetting selectSetting,
        IMusicRegistry musicRegistry,
        IAppBGMPlayer appBgmPlayer) {
        this.transitionService = transitionService;
        this.selectSetting = selectSetting;
        this.musicRegistry = musicRegistry;
        this.appBgmPlayer = appBgmPlayer;
    }

    void Start() {
        if (initialized) return;

        var musics = ResolveMusicList(musicRegistry);
        foreach (var stageSelectButton in stageSelectButtons) {
            stageSelectButton.Initialize(musics);
            stageSelectButton.OnStageSelected.Subscribe(OnStageSelected).AddTo(subscriptions);
            stageSelectButton.OnMusicSelected.Subscribe(OnMusicSelected).AddTo(subscriptions);
            stageSelectButton.OnPreviewVisibilityChanged.Subscribe(OnPreviewVisibilityChanged).AddTo(subscriptions);
        }

        backButton.OnBackPressed.Subscribe(_ => {
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

    void OnDestroy() {
        subscriptions.Dispose();
    }
}
