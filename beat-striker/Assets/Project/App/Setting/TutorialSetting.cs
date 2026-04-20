using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public record TutorialBattleSelection(Stage Stage, string MusicId, IReadOnlyList<StrikerSelectionRequest> PlayerSelections);

    public interface ITutorialSetting {
        TutorialBattleSelection BattleSelection { get; }
        bool IsTutorialBattleRequested { get; }
        void RequestTutorialBattle();
        void ClearTutorialBattleRequest();
    }

    public class TutorialSetting : MonoBehaviour, ITutorialSetting {
        [System.Serializable]
        class TutorialPlayerStrikerEntry {
            [SerializeField] int playerId;
            [SerializeField] Striker striker;

            public StrikerSelectionRequest ToSelectionRequest() {
                return new StrikerSelectionRequest(playerId, striker);
            }
        }

        [SerializeField] Stage stage = Stage.Live;
        [SerializeField] string musicId;
        [SerializeField] List<TutorialPlayerStrikerEntry> playerSelections = new();
        [SerializeField] bool isTutorialBattleRequested;

        public bool IsTutorialBattleRequested => isTutorialBattleRequested;

        public TutorialBattleSelection BattleSelection {
            get {
                var selections = new List<StrikerSelectionRequest>(playerSelections.Count);
                for (var i = 0; i < playerSelections.Count; i++) {
                    selections.Add(playerSelections[i].ToSelectionRequest());
                }

                return new TutorialBattleSelection(stage, musicId, selections);
            }
        }

        public void RequestTutorialBattle() {
            isTutorialBattleRequested = true;
        }

        public void ClearTutorialBattleRequest() {
            isTutorialBattleRequested = false;
        }
    }
}