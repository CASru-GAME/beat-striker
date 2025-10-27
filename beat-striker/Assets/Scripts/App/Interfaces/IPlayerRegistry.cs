


using System.Collections.Generic;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;

public interface IPlayerRegistry {
    PlayerId? ToPlayerId(GamePadId gamePadId);
    IEnumerable<PlayerId> GetAllPlayerIds();
}