
using System.Collections.Generic;
using Core.App.Types;
using UnityEditor.SceneManagement;

namespace Core.App.Models {
    public interface IBattleSettingModel {
        Stages Stage { get; set; }
        Track Track { get; set; }
        Strikers GetStriker(PlayerId playerId);
        void SetStriker(PlayerId playerId, Strikers striker);
    }
}