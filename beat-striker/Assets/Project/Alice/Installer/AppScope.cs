
using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Alice {
	public class AppScope : LifetimeScope {
		[SerializeField] PlayerInputManager playerInputManager;
		[SerializeField] SceneLoader sceneLoader;
		[SerializeField] StageRegistry stageRegistry;
		[SerializeField] MusicRegistry musicRegistry;
		[SerializeField] AppStrikerRegistry strikerRegistry;
		[SerializeField] BattleSelectSetting battleSelectSetting;
		[SerializeField] PlayerSelectSetting playerSelectSetting;
		[SerializeField] AudioSetting audioSetting;
		[SerializeField] BattleRuleSetting battleRuleSetting;
		[SerializeField] AppTransitionFactory appTransitionFactory;

		protected override void Awake() {
			base.Awake();
			DontDestroyOnLoad(gameObject);
		}

		protected override void Configure(IContainerBuilder builder) {
			builder.RegisterInstance<IStageRegistry>(stageRegistry);
			builder.RegisterInstance<IMusicRegistry>(musicRegistry);
			builder.RegisterInstance<IAppStrikerRegistry>(strikerRegistry);
			builder.RegisterInstance<IBattleSelectSetting>(battleSelectSetting);
			builder.RegisterInstance<IPlayerSelectSetting>(playerSelectSetting);
			builder.RegisterInstance<IAudioSetting>(audioSetting);
			builder.RegisterInstance<IBattleRuleSetting>(battleRuleSetting);
			builder.Register<IGamePadRegistry, GamePadRegistry>(Lifetime.Singleton);
			builder.RegisterInstance<IAppTransitionFactory>(appTransitionFactory);
			builder.RegisterInstance<ISceneLoader>(sceneLoader);
			builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);

			builder.RegisterInstance(playerInputManager);
			builder.RegisterEntryPoint<SceneInjectionHandler>(Lifetime.Singleton);
			builder.RegisterEntryPoint<PlayerJoinHandler>(Lifetime.Singleton);

			builder.RegisterBuildCallback(container => {
				_ = container.Resolve<IStageRegistry>();
				_ = container.Resolve<IMusicRegistry>();
				_ = container.Resolve<IAppStrikerRegistry>();
				_ = container.Resolve<IBattleSelectSetting>();
				_ = container.Resolve<IPlayerSelectSetting>();
				_ = container.Resolve<IAudioSetting>();
				_ = container.Resolve<IBattleRuleSetting>();
				_ = container.Resolve<IGamePadRegistry>();
				_ = container.Resolve<IAppTransitionFactory>();
				_ = container.Resolve<ISceneLoader>();
				_ = container.Resolve<ISceneTransitionService>();
				InjectSceneObjects(container, SceneManager.GetActiveScene());
			});
		}

		static void InjectSceneObjects(IObjectResolver container, Scene scene) {
			var rootObjects = scene.GetRootGameObjects();
			foreach (var root in rootObjects) {
				container.InjectGameObject(root);
			}
		}

		sealed class SceneInjectionHandler : IInitializable, IDisposable {
			readonly IObjectResolver container;

			public SceneInjectionHandler(IObjectResolver container) {
				this.container = container;
			}

			public void Initialize() {
				SceneManager.sceneLoaded += OnSceneLoaded;
			}

			public void Dispose() {
				SceneManager.sceneLoaded -= OnSceneLoaded;
			}

			void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
				InjectSceneObjects(container, scene);
			}
		}

		sealed class PlayerJoinHandler : IInitializable, IDisposable {
			readonly PlayerInputManager playerInputManager;
			readonly IObjectResolver container;

			public PlayerJoinHandler(PlayerInputManager playerInputManager, IObjectResolver container) {
				this.playerInputManager = playerInputManager;
				this.container = container;
			}

			public void Initialize() {
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