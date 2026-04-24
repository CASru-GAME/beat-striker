using R3;
using UnityEngine;

namespace Alice {
    public record StageSelectionRequest(Stage Stage);
    public record MusicSelectionRequest(string MusicId);

    public interface IBattleSelectSetting {
        ReadOnlyReactiveProperty<Stage> SelectedStage { get; }
        ReadOnlyReactiveProperty<string> SelectedMusicId { get; }
        int AiStrength { get; }
        void SelectStage(Stage stage);
        void SelectMusic(string musicId);
    }

    public class BattleSelectSetting : MonoBehaviour, IBattleSelectSetting {
        [SerializeField] Stage selectedStage;
        [SerializeField] string selectedMusicId;
        [SerializeField, Min(0)] int aiStrength = 1;

        readonly ReactiveProperty<Stage> selectedStageProperty = new();
        readonly ReactiveProperty<string> selectedMusicIdProperty = new();

        public ReadOnlyReactiveProperty<Stage> SelectedStage => selectedStageProperty;
        public ReadOnlyReactiveProperty<string> SelectedMusicId => selectedMusicIdProperty;
        public int AiStrength => Mathf.Max(0, aiStrength);

        void Awake() {
            selectedStageProperty.OnNext(selectedStage);
            selectedMusicIdProperty.OnNext(selectedMusicId);
        }

        public void SelectStage(Stage stage) {
            selectedStage = stage;
            selectedStageProperty.OnNext(stage);
        }

        public void SelectMusic(string musicId) {
            selectedMusicId = musicId;
            selectedMusicIdProperty.OnNext(musicId);
        }
    }
}
