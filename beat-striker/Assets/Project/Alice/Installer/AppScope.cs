
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Alice {
	[Serializable]
	public class AppSceneEntry {
		public AppScene Scene;
		public string SceneName;
	}

	[RequireComponent(typeof(PlayerInputManager))]
	public class AppScope : LifetimeScope {
		[SerializeField] StageRegistry stageRegistry;
		[SerializeField] MusicRegistry musicRegistry;
		[SerializeField] AppStrikerRegistry strikerRegistry;
		[SerializeField] AppTransitionFactory appTransitionFactory;
		[SerializeField] AppSceneEntry[] sceneEntries;
		[SerializeField] float defaultCommandTimeOffset;
		[SerializeField] float defaultViewTimeOffset;
		[SerializeField] float defaultBeatTimeOffset;
		[SerializeField] float defaultMasterVolume = 1f;
		[SerializeField] float defaultBgmVolume = 1f;
		[SerializeField] float defaultSeVolume = 1f;

		protected override void Awake() {
			base.Awake();
			DontDestroyOnLoad(gameObject);
		}

		protected override void Configure(IContainerBuilder builder) {
			var sceneMap = BuildSceneMap();

			var defaultBeatOffset = new BeatOffsetSetting(defaultCommandTimeOffset, defaultViewTimeOffset, defaultBeatTimeOffset);
			var defaultVolume = new VolumeBalance(defaultMasterVolume, defaultBgmVolume, defaultSeVolume);
			var appSettingsModel = new AppSettingsModel(stageRegistry.Default, musicRegistry.Default, defaultBeatOffset, defaultVolume);
			var playerSettingsModel = new PlayerSettingsModel(strikerRegistry.Default);

			builder.RegisterInstance<IReadOnlyDictionary<AppScene, string>>(sceneMap);
			builder.RegisterInstance<IStageRegistry>(stageRegistry);
			builder.RegisterInstance<IMusicRegistry>(musicRegistry);
			builder.RegisterInstance<IAppStrikerRegistry>(strikerRegistry);
			builder.RegisterInstance<IAppSettingsModel>(appSettingsModel);
			builder.RegisterInstance<IPlayerSettingsModel>(playerSettingsModel);
			builder.Register<IGamePadRegistry, GamePadRegistry>(Lifetime.Singleton);
			builder.RegisterInstance<IAppTransitionFactory>(appTransitionFactory);
			builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
			builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);

			builder.RegisterInstance(GetComponent<PlayerInputManager>());
			builder.RegisterEntryPoint<SceneInjectionHandler>(Lifetime.Singleton);
			builder.RegisterEntryPoint<PlayerJoinHandler>(Lifetime.Singleton);

			builder.RegisterBuildCallback(container => {
				_ = container.Resolve<IStageRegistry>();
				_ = container.Resolve<IMusicRegistry>();
				_ = container.Resolve<IAppStrikerRegistry>();
				_ = container.Resolve<IAppSettingsModel>();
				_ = container.Resolve<IPlayerSettingsModel>();
				_ = container.Resolve<IGamePadRegistry>();
				_ = container.Resolve<IAppTransitionFactory>();
				_ = container.Resolve<ISceneLoader>();
				_ = container.Resolve<ISceneTransitionService>();
				InjectSceneObjects(container, SceneManager.GetActiveScene());
			});
		}

		Dictionary<AppScene, string> BuildSceneMap() {
			var map = new Dictionary<AppScene, string>();
			foreach (var entry in sceneEntries) {
				map[entry.Scene] = entry.SceneName;
			}
			return map;
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