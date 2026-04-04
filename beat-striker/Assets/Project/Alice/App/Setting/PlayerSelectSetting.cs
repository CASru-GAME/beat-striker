using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Alice {
    public record StrikerSelectionRequest(int PlayerId, Striker Striker);
    public record PlayerStrikerSelectionChanged(int PlayerId, Striker Striker);

    [System.Serializable]
    public class PlayerStrikerDefaultEntry {
        public int PlayerId;
        public Striker Striker;
    }

    public interface IPlayerSelectSetting {
        ReadOnlyReactiveProperty<IReadOnlyDictionary<int, Striker>> SelectedStrikers { get; }
        Observable<PlayerStrikerSelectionChanged> OnPlayerStrikerSelected { get; }
        bool TryGetStriker(int playerId, out Striker striker);
        IReadOnlyDictionary<int, Striker> GetAllSelections();
        void SelectStriker(int playerId, Striker striker);
    }

    public class PlayerSelectSetting : MonoBehaviour, IPlayerSelectSetting {
        [SerializeField] List<PlayerStrikerDefaultEntry> defaultSelections = new();

        readonly Dictionary<int, Striker> selections = new();
        readonly Subject<PlayerStrikerSelectionChanged> playerStrikerSelected = new();
        readonly ReactiveProperty<IReadOnlyDictionary<int, Striker>> selectedStrikers = new();

        public ReadOnlyReactiveProperty<IReadOnlyDictionary<int, Striker>> SelectedStrikers => selectedStrikers;
        public Observable<PlayerStrikerSelectionChanged> OnPlayerStrikerSelected => playerStrikerSelected;

        void Awake() {
            selections.Clear();
            for (var i = 0; i < defaultSelections.Count; i++) {
                var entry = defaultSelections[i];
                selections[entry.PlayerId] = entry.Striker;
            }
            selectedStrikers.OnNext(new Dictionary<int, Striker>(selections));
        }

        public bool TryGetStriker(int playerId, out Striker striker) {
            return selections.TryGetValue(playerId, out striker);
        }

        public IReadOnlyDictionary<int, Striker> GetAllSelections() {
            return selections;
        }

        public void SelectStriker(int playerId, Striker striker) {
            selections[playerId] = striker;
            selectedStrikers.OnNext(new Dictionary<int, Striker>(selections));
            playerStrikerSelected.OnNext(new PlayerStrikerSelectionChanged(playerId, striker));
        }
    }
}
