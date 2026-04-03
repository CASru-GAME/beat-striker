using R3;
using UnityEngine;

namespace Alice {
    public record StageSelectionRequest(string StageId);
    public record MusicSelectionRequest(string MusicId);

    public interface IBattleSelectSetting {
        ReadOnlyReactiveProperty<string> SelectedStageId { get; }
        ReadOnlyReactiveProperty<string> SelectedMusicId { get; }
        void SelectStage(string stageId);
        void SelectMusic(string musicId);
    }

    public class BattleSelectSetting : MonoBehaviour, IBattleSelectSetting {
        [SerializeField] string selectedStageId;
        [SerializeField] string selectedMusicId;

        readonly ReactiveProperty<string> selectedStageIdProperty = new();
        readonly ReactiveProperty<string> selectedMusicIdProperty = new();

        public ReadOnlyReactiveProperty<string> SelectedStageId => selectedStageIdProperty;
        public ReadOnlyReactiveProperty<string> SelectedMusicId => selectedMusicIdProperty;

        void Awake() {
            selectedStageIdProperty.OnNext(selectedStageId);
            selectedMusicIdProperty.OnNext(selectedMusicId);
        }

        public void SelectStage(string stageId) {
            selectedStageId = stageId;
            selectedStageIdProperty.OnNext(stageId);
        }

        public void SelectMusic(string musicId) {
            selectedMusicId = musicId;
            selectedMusicIdProperty.OnNext(musicId);
        }
    }
}
