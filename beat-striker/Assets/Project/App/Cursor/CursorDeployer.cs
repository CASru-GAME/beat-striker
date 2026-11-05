using System;
using System.Collections.Generic;
using R3;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alice {
    public interface ICursorDeployer {
        void SetForceEnabled(bool enabled);
    }

    public class CursorDeployer : ICursorDeployer, IInitializable, IDisposable {
        const int MAXPLAYERS = 4;

        readonly IGamePadRegistry gamePadRegistry;
        readonly ICursorFactory cursorFactory;
        readonly IScreenRegistry screenRegistry;
        readonly ICursorMoveSetting cursorMoveSetting;
        readonly IInputFocusService inputFocus;

        readonly List<IDisposable> playerJoinSubscriptions = new();
        readonly Dictionary<int, DeployedCursor> deployedByPlayerId = new();

        bool isCursorEnabled;
        bool forceEnabled;

        bool IsDeploymentEnabled => isCursorEnabled || forceEnabled;

        [Inject]
        public CursorDeployer(
            IGamePadRegistry gamePadRegistry,
            ICursorFactory cursorFactory,
            IScreenRegistry screenRegistry,
            ICursorMoveSetting cursorMoveSetting,
            IInputFocusService inputFocus) {
            this.gamePadRegistry = gamePadRegistry;
            this.cursorFactory = cursorFactory;
            this.screenRegistry = screenRegistry;
            this.cursorMoveSetting = cursorMoveSetting;
            this.inputFocus = inputFocus;
        }

        public void Initialize() {
            ApplyCursorRule(SceneManager.GetActiveScene().name);
            SceneManager.sceneLoaded += OnSceneLoaded;

            for (var i = 0; i < MAXPLAYERS; i++) {
                var playerId = i;
                var playerGamePad = gamePadRegistry.Get(playerId);

                if (playerGamePad.HasGamePad.CurrentValue && IsDeploymentEnabled) {
                    Deploy(playerId, playerGamePad);
                }

                var hasGamePadSubscription = playerGamePad.HasGamePad.Subscribe(hasGamePad => {
                    if (hasGamePad && IsDeploymentEnabled) {
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

        public void SetForceEnabled(bool enabled) {
            if (forceEnabled == enabled) {
                return;
            }

            forceEnabled = enabled;
            ApplyDeploymentState();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            ApplyCursorRule(scene.name);
        }

        void ApplyCursorRule(string sceneName) {
            if (!screenRegistry.TryGetBySceneName(sceneName, out var screenInfo)) {
                screenInfo = screenRegistry.Default;
                Debug.LogWarning($"[CursorDeployer] Unknown scene in ScreenRegistry. sceneName={sceneName}, fallbackScene={screenInfo.SceneName}");
            }

            isCursorEnabled = screenInfo.CreateCursor;
            ApplyDeploymentState();
        }

        void ApplyDeploymentState() {
            if (!IsDeploymentEnabled) {
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
            cursor.SetSpeedScale(cursorMoveSetting.CursorSpeed.CurrentValue);

            var directionSubscription = playerGamePad.OnCursorDirection.Subscribe(direction => {
                if (inputFocus.HasTextFocus && playerGamePad.DeviceName == "Keybord") {
                    cursor.StopMove();
                    return;
                }
                if (direction == Vector2.zero) {
                    cursor.StopMove();
                    return;
                }

                cursor.SetDirection(direction);
            });

            var directionCanceledSubscription = playerGamePad.OnCursorDirection.Where(direction => direction == Vector2.zero).Subscribe(_ => {
                cursor.StopMove();
            });

            var buttonDownSubscription = inputFocus.RegisterCursor(playerId, cursor.Click);

            var speedSubscription = cursorMoveSetting.CursorSpeed.Subscribe(cursor.SetSpeedScale);
            var inputStateSubscription = new CompositeDisposable();
            inputFocus.Changed.Subscribe(_ => {
                if (inputFocus.HasTextFocus && playerGamePad.DeviceName == "Keybord") cursor.StopMove();
            }).AddTo(inputStateSubscription);
            inputFocus.TextFocusChanged.Subscribe(active => {
                if (active && playerGamePad.DeviceName == "Keybord") cursor.StopMove();
            }).AddTo(inputStateSubscription);

            deployedByPlayerId[playerId] = new DeployedCursor(
                cursor,
                directionSubscription,
                directionCanceledSubscription,
                buttonDownSubscription,
                speedSubscription,
                inputStateSubscription);
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
            readonly IDisposable speedSubscription;
            readonly IDisposable inputStateSubscription;

            public DeployedCursor(
                ICursor cursor,
                IDisposable directionSubscription,
                IDisposable directionCanceledSubscription,
                IDisposable buttonDownSubscription,
                IDisposable speedSubscription,
                IDisposable inputStateSubscription) {
                this.cursor = cursor;
                this.directionSubscription = directionSubscription;
                this.directionCanceledSubscription = directionCanceledSubscription;
                this.buttonDownSubscription = buttonDownSubscription;
                this.speedSubscription = speedSubscription;
                this.inputStateSubscription = inputStateSubscription;
            }

            public void Dispose() {
                directionSubscription.Dispose();
                directionCanceledSubscription.Dispose();
                buttonDownSubscription.Dispose();
                speedSubscription.Dispose();
                inputStateSubscription.Dispose();
                cursor.DestroyCursor();
            }
        }
    }
}
