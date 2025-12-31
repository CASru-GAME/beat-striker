


using System;
using System.Collections.Generic;
using Core.App.Interfaces;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.GamePad;
using Core.GamePad.Models;
using Core.GamePad.Types;
using Core.Utils;
using UnityEngine;

namespace Core.App {
    public class PlayerRegistry : IPlayerRegistry {
        private const int MAXPLAYERS = 100;
        private readonly Dictionary<int, PlayerId> playerMap = new();
        private readonly IAppModel appModel;
        private readonly IGamePadInputModel gamePadInputModel;
        private readonly CompositeDisposable subscriptions = new();

        public PlayerRegistry(IAppModel appModel, IGamePadInputModel gamePadInputModel, ILife life) {
            Debug.Log("PlayerRegistry Constructor");
            this.appModel = appModel;
            this.gamePadInputModel = gamePadInputModel;
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
            subscriptions.Add(gamePadInputModel.SubscribeJoined(OnGamePadJoined));
            subscriptions.Add(gamePadInputModel.SubscribeLeft(OnGamePadLeft));
        }

        public void OnDisable() {
            subscriptions.Dispose();
        }

        private void OnGamePadJoined(GamePadId gamePadId) {
            Debug.Log($"GamePad Joined: {gamePadId.value}");
            for (int playerIdValue = 0; playerIdValue < MAXPLAYERS; playerIdValue++) {
                var pid = new PlayerId(playerIdValue);
                if (!playerMap.ContainsValue(pid)) {
                    playerMap[gamePadId.value] = pid;
                    appModel.FirePlayerJoined(pid);
                    break;
                }
            }
        }

        private void OnGamePadLeft(GamePadId gamePadId) {
            var playerId = ToPlayerId(gamePadId);
            if (playerId == null) return;

            playerMap.Remove(gamePadId.value);

            bool otherGamePadExists = false;
            foreach (var mappedPlayerId in playerMap.Values) {
                if (mappedPlayerId == playerId.Value) {
                    otherGamePadExists = true;
                    break;
                }
            }

            if (!otherGamePadExists) {
                appModel.FirePlayerLeft(playerId.Value);
            }
        }

        public IEnumerable<PlayerId> GetAllPlayerIds() {
            return new HashSet<PlayerId>(playerMap.Values);
        }
    }
}