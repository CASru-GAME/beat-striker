
using System.Collections.Generic;
using Core.App.Types;

namespace Core.App.Models {
    public interface IBattleSettingModel {
        StageId Stage { get; set; }
        TrackId Track { get; set; }
        StrikerId? GetStriker(PlayerId playerId);
        void SetStriker(PlayerId playerId, StrikerId? striker);
    }
}