

using System.Collections.Generic;
using Core.App.Types;

namespace Core.App.Models {

    public class BattleSettingModel: IBattleSettingModel{
        public Stages Stage { get; set; }
        public Track Track { get; set; }
        private readonly Dictionary<int, Strikers> strikers = new();

        public Strikers GetStriker(PlayerId playerId) {
            return strikers.ContainsKey(playerId.value) ? strikers[playerId.value] : Strikers.None;
        }

        public void SetStriker(PlayerId playerId, Strikers striker) {
            strikers[playerId.value] = striker;
        }
        
    }
}