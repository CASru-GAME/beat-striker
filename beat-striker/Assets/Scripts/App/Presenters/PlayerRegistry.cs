


using System.Collections.Generic;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;

public class PlayerRegistry : IPlayerRegistry {
    private const int MAXPLAYERS = 100;
    private readonly Dictionary<int, PlayerId> playerMap = new();
    private readonly IBus bus;

    public PlayerRegistry(IBus bus, ILife life) {
        this.bus = bus;
        life.Link(OnEnable, OnDisable);
    }

    public PlayerId? ToPlayerId(GamePadId gamePadId) {
        if (playerMap.TryGetValue(gamePadId.value, out var playerId)) {
            return playerId;
        }
        return null;
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
            var pid = new PlayerId(playerIdValue);
            if (!playerMap.ContainsValue(pid)) {
                playerMap[message.gamePadId.value] = pid;
                bus.Publish(new AppMessages.PlayerJoined(pid));
                break;
            }
        }
    }

    private void OnGamePadLeft(GamePadMessages.Left message) {
        var playerId = ToPlayerId(message.gamePadId);
        if (playerId == null) return;
        
        playerMap.Remove(message.gamePadId.value);
        
        bool otherGamePadExists = false;
        foreach (var mappedPlayerId in playerMap.Values) {
            if (mappedPlayerId == playerId.Value) {
                otherGamePadExists = true;
                break;
            }
        }
        
        if (!otherGamePadExists) {
            bus.Publish(new AppMessages.PlayerLeft(playerId.Value));
        }
    }

    public IEnumerable<PlayerId> GetAllPlayerIds() {
        return new HashSet<PlayerId>(playerMap.Values);
    }
}