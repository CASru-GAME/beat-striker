
using System;
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

        protected override void Configure(IContainerBuilder builder) {
            builder.Register<IGamePadRegistry, GamePadRegistry>(Lifetime.Singleton);
            builder.Register<IStrikerRegistry, StrikerRegistry>(Lifetime.Singleton);
            builder.Register<IStrikerFactory, StrikerHubFactory>(Lifetime.Singleton);
            builder.Register<IBattleDeployer, BattleDeployer>(Lifetime.Singleton);
            builder.Register<IBattleFlow, BattleFlow>(Lifetime.Singleton);
            builder.Register<IBeatjudge, BeatJudge>(Lifetime.Singleton);
            builder.Register<IMusicPlayer, MusicPlayer>(Lifetime.Singleton);

            builder.RegisterInstance(GetComponent<PlayerInputManager>());
            builder.RegisterInstance(GetComponent<BattleConfig>());
            builder.RegisterInstance(GetComponent<BeatConfig>());
            builder.RegisterInstance(GetComponent<AudioSource>());
            builder.RegisterEntryPoint<PlayerJoinHandler>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BattleStartHandler>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container => {
                _ = container.Resolve<IGamePadRegistry>();
                _ = container.Resolve<IStrikerRegistry>();
                _ = container.Resolve<IStrikerFactory>();
                _ = container.Resolve<IBattleDeployer>();
                _ = container.Resolve<IBattleFlow>();
                _ = container.Resolve<IBeatjudge>();
                _ = container.Resolve<IMusicPlayer>();
            });
        }

        sealed class PlayerJoinHandler : IInitializable, IDisposable {
            readonly PlayerInputManager playerInputManager;
            readonly IObjectResolver container;

            public PlayerJoinHandler(PlayerInputManager playerInputManager, IObjectResolver container) {
                this.playerInputManager = playerInputManager;
                this.container = container;
            }

            public void Initialize() {
                playerInputManager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
                playerInputManager.onPlayerJoined += OnPlayerJoined;
            }

            public void Dispose() {
                playerInputManager.onPlayerJoined -= OnPlayerJoined;
            }

            void OnPlayerJoined(PlayerInput playerInput) {
                container.InjectGameObject(playerInput.gameObject);
            }
        }

    }
}