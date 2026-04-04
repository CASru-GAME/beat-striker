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
    readonly CompositeDisposable subscriptions = new();
    bool initialized;

    [Inject]
    public void Construct(
        ISceneTransitionService transitionService,
        IBattleSelectSetting selectSetting,
        IMusicRegistry musicRegistry) {
        this.transitionService = transitionService;
        this.selectSetting = selectSetting;
        this.musicRegistry = musicRegistry;
    }

    void Start() {
        if (initialized) return;

        var musics = ResolveMusicList(musicRegistry);
        foreach (var stageSelectButton in stageSelectButtons) {
            stageSelectButton.Initialize(musics);
            stageSelectButton.OnStageSelected.Subscribe(OnStageSelected).AddTo(subscriptions);
            stageSelectButton.OnMusicSelected.Subscribe(OnMusicSelected).AddTo(subscriptions);
        }

        backButton.OnBackPressed.Subscribe(_ => {
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
        transitionService.RequestStartTransition(AppScene.CharacterSelect);
    }

    static IReadOnlyList<MusicInfo> ResolveMusicList(IMusicRegistry musicRegistry) {
        return (IReadOnlyList<MusicInfo>)musicRegistry.GetType().GetMethod("GetAll").Invoke(musicRegistry, null);
    }

    void OnDestroy() {
        subscriptions.Dispose();
    }
}
