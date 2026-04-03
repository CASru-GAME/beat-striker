using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Alice {
    public record StrikerSelectionRequest(int PlayerId, string StrikerId);
    public record PlayerStrikerIdSelection(int PlayerId, string StrikerId);

    [System.Serializable]
    public class PlayerStrikerDefaultEntry {
        public int PlayerId;
        public string StrikerId;
    }

    public interface IPlayerSelectSetting {
        ReadOnlyReactiveProperty<IReadOnlyDictionary<int, string>> SelectedStrikerIds { get; }
        Observable<PlayerStrikerIdSelection> OnPlayerStrikerSelected { get; }
        string GetStrikerId(int playerId);
        IReadOnlyDictionary<int, string> GetAllSelections();
        void SelectStriker(int playerId, string strikerId);
    }

    public class PlayerSelectSetting : MonoBehaviour, IPlayerSelectSetting {
        [SerializeField] List<PlayerStrikerDefaultEntry> defaultSelections = new();

        readonly Dictionary<int, string> selections = new();
        readonly Subject<PlayerStrikerIdSelection> playerStrikerSelected = new();
        readonly ReactiveProperty<IReadOnlyDictionary<int, string>> selectedStrikerIds = new();

        public ReadOnlyReactiveProperty<IReadOnlyDictionary<int, string>> SelectedStrikerIds => selectedStrikerIds;
        public Observable<PlayerStrikerIdSelection> OnPlayerStrikerSelected => playerStrikerSelected;

        void Awake() {
            selections.Clear();
            for (var i = 0; i < defaultSelections.Count; i++) {
                var entry = defaultSelections[i];
                selections[entry.PlayerId] = entry.StrikerId;
            }
            selectedStrikerIds.OnNext(new Dictionary<int, string>(selections));
        }

        public string GetStrikerId(int playerId) {
            if (selections.TryGetValue(playerId, out var selectedId)) {
                return selectedId;
            }
            return string.Empty;
        }

        public IReadOnlyDictionary<int, string> GetAllSelections() {
            return selections;
        }

        public void SelectStriker(int playerId, string strikerId) {
            selections[playerId] = strikerId;
            selectedStrikerIds.OnNext(new Dictionary<int, string>(selections));
            playerStrikerSelected.OnNext(new PlayerStrikerIdSelection(playerId, strikerId));
        }
    }
}
