


using System.Collections.Generic;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;

public class PlayerRegistry: IPlayerRegistry {
    private const int MAXPLAYERS = 100;
    private readonly Dictionary<int, PlayerId> playerMap = new();
    private readonly IBus bus;

    public PlayerRegistry(IBus bus, ILife life) {
        this.bus = bus;
        life.Link(OnEnable, OnDisable);
    }

    public PlayerId ToPlayerId(GamePadId gamePadId) {
        playerMap.TryGetValue(gamePadId.value, out var playerId);
        return playerId;
    }

    public void OnEnable() {
        bus.Subscribe<GamePadMessages.Joined>(OnGamePadJoined);
        bus.Subscribe<GamePadMessages.Left>(OnGamePadLeft);
    }

    public void OnDisable() {
        bus.Unsubscribe<GamePadMessages.Joined>(OnGamePadJoined);
        bus.Unsubscribe<GamePadMessages.Left>(OnGamePadLeft);
    }

    private void OnGamePadJoined(GamePadMessages.Joined message) {
        for (int playerIdValue = 0; playerIdValue < MAXPLAYERS; playerIdValue++) {
            if (!playerMap.ContainsValue(new PlayerId(playerIdValue))) {
                playerMap[message.gamePadId.value] = new PlayerId(playerIdValue);
                break;
            }
        }
    }

    private void OnGamePadLeft(GamePadMessages.Left message) {
        playerMap.Remove(message.gamePadId.value);
    }


}