using System;
using System.Collections.Generic;
using R3;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alice {
    public class CursorDeployer : IInitializable, IDisposable {
        const int MAXPLAYERS = 4;

        readonly IGamePadRegistry gamePadRegistry;
        readonly ICursorFactory cursorFactory;
        readonly IScreenRegistry screenRegistry;

        readonly List<IDisposable> playerJoinSubscriptions = new();
        readonly Dictionary<int, DeployedCursor> deployedByPlayerId = new();
        bool isCursorEnabled;

        public CursorDeployer(
            IGamePadRegistry gamePadRegistry,
            ICursorFactory cursorFactory,
            IScreenRegistry screenRegistry) {
            this.gamePadRegistry = gamePadRegistry;
            this.cursorFactory = cursorFactory;
            this.screenRegistry = screenRegistry;
        }

        public void Initialize() {
            ApplyCursorRule(SceneManager.GetActiveScene().name);
            SceneManager.sceneLoaded += OnSceneLoaded;

            for (var i = 0; i < MAXPLAYERS; i++) {
                var playerId = i;
                var playerGamePad = gamePadRegistry.Get(playerId);

                if (playerGamePad.HasGamePad.CurrentValue && isCursorEnabled) {
                    Deploy(playerId, playerGamePad);
                }

                var hasGamePadSubscription = playerGamePad.HasGamePad.Subscribe(hasGamePad => {
                    if (hasGamePad && isCursorEnabled) {
                        Deploy(playerId, playerGamePad);
                    }
                    else {
                        Undeploy(playerId);
                    }
                });

                playerJoinSubscriptions.Add(hasGamePadSubscription);
            }
        }

        public void Dispose() {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            for (var i = 0; i < playerJoinSubscriptions.Count; i++) {
                playerJoinSubscriptions[i].Dispose();
            }
            playerJoinSubscriptions.Clear();

            var deployed = new List<int>(deployedByPlayerId.Keys);
            for (var i = 0; i < deployed.Count; i++) {
                Undeploy(deployed[i]);
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            ApplyCursorRule(scene.name);
        }

        void ApplyCursorRule(string sceneName) {
            var screenInfo = screenRegistry.GetBySceneName(sceneName);
            isCursorEnabled = screenInfo.CreateCursor;

            if (!isCursorEnabled) {
                UndeployAll();
                return;
            }

            DeployConnectedPlayers();
        }

        void DeployConnectedPlayers() {
            for (var playerId = 0; playerId < MAXPLAYERS; playerId++) {
                var playerGamePad = gamePadRegistry.Get(playerId);
                if (playerGamePad.HasGamePad.CurrentValue) {
                    Deploy(playerId, playerGamePad);
                }
            }
        }

        void UndeployAll() {
            var deployed = new List<int>(deployedByPlayerId.Keys);
            for (var i = 0; i < deployed.Count; i++) {
                Undeploy(deployed[i]);
            }
        }

        void Deploy(int playerId, IPlayerGamePad playerGamePad) {
            if (deployedByPlayerId.ContainsKey(playerId)) {
                return;
            }

            var cursor = cursorFactory.Create(playerId);
            var directionSubscription = playerGamePad.OnDirection.Subscribe(direction => {
                if (direction == Vector2.zero) {
                    cursor.StopMove();
                    return;
                }

                cursor.SetDirection(direction);
            });

            var directionCanceledSubscription = playerGamePad.OnDirectionCanceled.Subscribe(_ => {
                cursor.StopMove();
            });

            var buttonDownSubscription = playerGamePad.OnButtonDown.Subscribe(button => {
                if (button == GamePadButton.East) {
                    cursor.Click();
                }
            });

            deployedByPlayerId[playerId] = new DeployedCursor(
                cursor,
                directionSubscription,
                directionCanceledSubscription,
                buttonDownSubscription);
        }

        void Undeploy(int playerId) {
            if (!deployedByPlayerId.TryGetValue(playerId, out var deployed)) {
                return;
            }

            deployed.Dispose();
            deployedByPlayerId.Remove(playerId);
        }

        class DeployedCursor : IDisposable {
            readonly ICursor cursor;
            readonly IDisposable directionSubscription;
            readonly IDisposable directionCanceledSubscription;
            readonly IDisposable buttonDownSubscription;

            public DeployedCursor(
                ICursor cursor,
                IDisposable directionSubscription,
                IDisposable directionCanceledSubscription,
                IDisposable buttonDownSubscription) {
                this.cursor = cursor;
                this.directionSubscription = directionSubscription;
                this.directionCanceledSubscription = directionCanceledSubscription;
                this.buttonDownSubscription = buttonDownSubscription;
            }

            public void Dispose() {
                directionSubscription.Dispose();
                directionCanceledSubscription.Dispose();
                buttonDownSubscription.Dispose();
                cursor.DestroyCursor();
            }
        }
    }
}