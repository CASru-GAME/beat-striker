

using System.Collections.Generic;
using Core.App.Types;

namespace Core.App.Models {

    public class BattleSettingModel: IBattleSettingModel{
        public StageId Stage { get; set; }
        public TrackId Track { get; set; }
        private readonly StrikerId defaultStrikerId = new("");
        private readonly Dictionary<int, StrikerId> strikers = new();

        public BattleSettingModel(StageId stage, TrackId track, StrikerId defaultStrikerId) {
            this.Stage = stage;
            this.Track = track;
            this.defaultStrikerId = defaultStrikerId;
        }

        public StrikerId GetStriker(PlayerId playerId) {
            return strikers.ContainsKey(playerId.value) ? strikers[playerId.value] : defaultStrikerId;
        }

        public void SetStriker(PlayerId playerId, StrikerId striker) {
            strikers[playerId.value] = striker;
        }
        
    }
}