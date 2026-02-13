


using System.Collections.Generic;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;
using UnityEngine;

namespace Core.App{
    public class PlayerRegistry : IPlayerRegistry {
        private const int MAXPLAYERS = 100;
        private readonly Dictionary<int, PlayerId> playerMap = new();
        private readonly IBus bus;

        public PlayerRegistry(IBus bus, ILife life) {
            Debug.Log("PlayerRegistry Constructor");
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
            Debug.Log("PlayerRegistry OnEnable");
            bus.Subscribe<GamePadMessages.Joined>(OnGamePadJoined);
            bus.Subscribe<AppMessages.JoinedWithPlayerId>(OnGamePadJoinedWithPlayerId);
            bus.Subscribe<GamePadMessages.Left>(OnGamePadLeft);
        }

        public void OnDisable() {
            bus.Unsubscribe<GamePadMessages.Joined>(OnGamePadJoined);
            bus.Unsubscribe<AppMessages.JoinedWithPlayerId>(OnGamePadJoinedWithPlayerId);
            bus.Unsubscribe<GamePadMessages.Left>(OnGamePadLeft);
        }

        private void OnGamePadJoined(GamePadMessages.Joined message) {
            Debug.Log($"GamePad Joined: {message.gamePadId.value}");
            for (int playerIdValue = 0; playerIdValue < MAXPLAYERS; playerIdValue++) {
                var pid = new PlayerId(playerIdValue);
                if (!playerMap.ContainsValue(pid)) {
                    playerMap[message.gamePadId.value] = pid;
                    bus.Publish(new AppMessages.PlayerJoined(pid));
                    break;
                }
            }
        }

        private void OnGamePadJoinedWithPlayerId(AppMessages.JoinedWithPlayerId message) {
            Debug.Log($"GamePad Joined with PlayerId: {message.gamePadId.value} -> {message.playerId.value}");
            playerMap[message.gamePadId.value] = message.playerId;
            bus.Publish(new AppMessages.PlayerJoined(message.playerId));
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
}