
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Alice {
    
    [RequireComponent(typeof(BattleConfig))]
    [RequireComponent(typeof(PlayerInputManager))]
    [RequireComponent(typeof(BeatConfig))]
    [RequireComponent(typeof(AudioSource))]
    public class AliceScope : LifetimeScope {
        [SerializeField] BattlePresenter battlePresenter;
        [SerializeField] BattlePlayerPresenter[] battlePlayerPresenters;

        protected override void Configure(IContainerBuilder builder) {
            builder.Register<IGamePadRegistry, GamePadRegistry>(Lifetime.Singleton);
            builder.Register<IStrikerRegistry, StrikerRegistry>(Lifetime.Singleton);
            builder.Register<IStrikerFactory, StrikerHubFactory>(Lifetime.Singleton);
            builder.Register<IBattleDeployer, BattleDeployer>(Lifetime.Singleton);
            builder.Register<IBattleJudge, BattleJudge>(Lifetime.Singleton);
            builder.RegisterInstance<IBattlePresenter>(battlePresenter);
            var battlePlayerPresenters = new IBattlePlayerPresenter[this.battlePlayerPresenters.Length];
            for (var i = 0; i < this.battlePlayerPresenters.Length; i++) {
                battlePlayerPresenters[i] = this.battlePlayerPresenters[i];
                builder.RegisterInstance<IBattlePlayerPresenter>(this.battlePlayerPresenters[i]);
            }
            builder.RegisterInstance(battlePlayerPresenters);
            builder.Register<IBattleFlow, BattleFlow>(Lifetime.Singleton);
            builder.Register<IBeatjudge, BeatJudge>(Lifetime.Singleton);
            builder.Register<IMusicPlayer, MusicPlayer>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BattleFlowStarter>(Lifetime.Singleton);

            builder.RegisterInstance(GetComponent<PlayerInputManager>());
            builder.RegisterInstance(GetComponent<BattleConfig>());
            builder.RegisterInstance(GetComponent<BeatConfig>());
            builder.RegisterInstance(GetComponent<AudioSource>());
            builder.RegisterEntryPoint<PlayerJoinHandler>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container => {
                _ = container.Resolve<IGamePadRegistry>();
                _ = container.Resolve<IStrikerRegistry>();
                _ = container.Resolve<IStrikerFactory>();
                _ = container.Resolve<IBattleDeployer>();
                _ = container.Resolve<IBattleJudge>();
                _ = container.Resolve<IBattleFlow>();
                _ = container.Resolve<IBattlePlayerPresenter[]>();
                _ = container.Resolve<IBeatjudge>();
                _ = container.Resolve<IMusicPlayer>();
                InjectSceneObjects(container);
            });
        }

        void InjectSceneObjects(IObjectResolver container) {
            var rootObjects = gameObject.scene.GetRootGameObjects();
            foreach (var root in rootObjects) {
                container.InjectGameObject(root);
            }
        }

        sealed class PlayerJoinHandler : IInitializable, IDisposable, ITickable {
            readonly PlayerInputManager playerInputManager;
            readonly IObjectResolver container;
            readonly HashSet<int> joinDebounce = new();

            public PlayerJoinHandler(PlayerInputManager playerInputManager, IObjectResolver container) {
                this.playerInputManager = playerInputManager;
                this.container = container;
            }

            public void Initialize() {
                playerInputManager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
                playerInputManager.joinBehavior = PlayerJoinBehavior.JoinPlayersManually;
                playerInputManager.onPlayerJoined += OnPlayerJoined;
            }

            public void Dispose() {
                playerInputManager.onPlayerJoined -= OnPlayerJoined;
            }

            void OnPlayerJoined(PlayerInput playerInput) {
                container.InjectGameObject(playerInput.gameObject);
            }

            public void Tick() {
                TryJoinKeyboard();
                TryJoinGamepads();
            }

            void TryJoinKeyboard() {
                var keyboard = Keyboard.current;
                if (keyboard == null) {
                    return;
                }

                if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && !IsPaired(keyboard)) {
                    playerInputManager.JoinPlayer(pairWithDevice: keyboard, controlScheme: "Keybord");
                }
            }

            void TryJoinGamepads() {
                foreach (var gamepad in Gamepad.all) {
                    if (gamepad == null) {
                        continue;
                    }

                    var deviceId = gamepad.deviceId;
                    var pressed = gamepad.startButton.isPressed || gamepad.buttonSouth.isPressed;

                    if (!pressed) {
                        joinDebounce.Remove(deviceId);
                        continue;
                    }

                    if (joinDebounce.Contains(deviceId) || IsPaired(gamepad)) {
                        continue;
                    }

                    playerInputManager.JoinPlayer(pairWithDevice: gamepad);
                    joinDebounce.Add(deviceId);
                }
            }

            static bool IsPaired(InputDevice device) {
                foreach (var player in PlayerInput.all) {
                    foreach (var pairedDevice in player.devices) {
                        if (pairedDevice == device) {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        sealed class BattleFlowStarter : IInitializable {
            readonly IBattleFlow battleFlow;

            public BattleFlowStarter(IBattleFlow battleFlow) {
                this.battleFlow = battleFlow;
            }

            public void Initialize() {
                battleFlow.StartBattle();
            }
        }

    }
}