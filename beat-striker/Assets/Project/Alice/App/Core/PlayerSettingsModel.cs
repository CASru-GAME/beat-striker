using System.Collections.Generic;
using R3;

namespace Alice {
    public interface IPlayerSettingsModel {
        Observable<PlayerStrikerSelection> OnPlayerStrikerSelected { get; }
        StrikerInfo GetStriker(int playerId);
        IReadOnlyDictionary<int, StrikerInfo> GetAllSelections();
        void SelectStriker(int playerId, StrikerInfo striker);
    }
    
    public class PlayerSettingsModel : IPlayerSettingsModel {
        readonly Dictionary<int, StrikerInfo> selections = new();
        readonly StrikerInfo defaultStriker;
        readonly Subject<PlayerStrikerSelection> playerStrikerSelected = new();

        public Observable<PlayerStrikerSelection> OnPlayerStrikerSelected => playerStrikerSelected;

        public PlayerSettingsModel(StrikerInfo defaultStriker) {
            this.defaultStriker = defaultStriker;
        }

        public StrikerInfo GetStriker(int playerId) {
            if (selections.TryGetValue(playerId, out var selected)) {
                return selected;
            }
            return defaultStriker;
        }

        public IReadOnlyDictionary<int, StrikerInfo> GetAllSelections() {
            return selections;
        }

        public void SelectStriker(int playerId, StrikerInfo striker) {
            selections[playerId] = striker;
            playerStrikerSelected.OnNext(new PlayerStrikerSelection(playerId, striker));
        }
    }
}
