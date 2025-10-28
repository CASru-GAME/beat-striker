


using System.Collections.Generic;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;

namespace Core.App.Interfaces {
    public interface IPlayerRegistry {
        PlayerId? ToPlayerId(GamePadId gamePadId);
        IEnumerable<PlayerId> GetAllPlayerIds();
    }
}