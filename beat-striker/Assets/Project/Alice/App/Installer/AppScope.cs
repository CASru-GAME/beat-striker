
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Alice {
	public class AppScope : LifetimeScope {
		static AppScope instance;
		readonly HashSet<int> injectedSceneHandles = new();

		[SerializeField] PlayerInputManager playerInputManager;
		[SerializeField] SceneLoader sceneLoader;
		[SerializeField] StageRegistry stageRegistry;
		[SerializeField] ScreenRegistry screenRegistry;
		[SerializeField] MusicRegistry musicRegistry;
		[SerializeField] AppStrikerRegistry strikerRegistry;
		[SerializeField] BattleSelectSetting battleSelectSetting;
		[SerializeField] PlayerSelectSetting playerSelectSetting;
		[SerializeField] AudioSetting audioSetting;
		[SerializeField] BattleRuleSetting battleRuleSetting;
		[SerializeField] AppTransitionFactory appTransitionFactory;
		[SerializeField] CursorFactory cursorFactory;
		[SerializeField] AppBGMPlayer appBgmPlayer;

		protected override void Awake() {
			if (instance != null && instance != this) {
				Destroy(gameObject);
				return;
			}

			instance = this;
			DontDestroyOnLoad(gameObject);
			base.Awake();
		}

		protected override void OnDestroy() {
			if (instance == this) {
				instance = null;
			}

			base.OnDestroy();
		}

		protected override void Configure(IContainerBuilder builder) {
			builder.RegisterInstance<IStageRegistry>(stageRegistry);
			builder.RegisterInstance<IScreenRegistry>(screenRegistry);
			builder.RegisterInstance<IMusicRegistry>(musicRegistry);
			builder.RegisterInstance<IAppStrikerRegistry>(strikerRegistry);
			builder.RegisterInstance<IBattleSelectSetting>(battleSelectSetting);
			builder.RegisterInstance<IPlayerSelectSetting>(playerSelectSetting);
			builder.RegisterInstance<IAudioSetting>(audioSetting);
			builder.RegisterInstance<IBattleRuleSetting>(battleRuleSetting);
			builder.Register<IGamePadRegistry, GamePadRegistry>(Lifetime.Singleton);
			builder.RegisterInstance<IAppTransitionFactory>(appTransitionFactory);
			builder.RegisterInstance<ICursorFactory>(cursorFactory);
			builder.RegisterInstance<IAppBGMPlayer>(appBgmPlayer);
			builder.RegisterInstance<ISceneLoader>(sceneLoader);
			builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);

			builder.RegisterInstance(playerInputManager);
			builder.RegisterEntryPoint<SceneInjectionHandler>(Lifetime.Singleton);
			builder.RegisterEntryPoint<PlayerJoinHandler>(Lifetime.Singleton);
			builder.RegisterEntryPoint<CursorDeployer>(Lifetime.Singleton);

			builder.RegisterBuildCallback(container => {
				_ = container.Resolve<IStageRegistry>();
				_ = container.Resolve<IScreenRegistry>();
				_ = container.Resolve<IMusicRegistry>();
				_ = container.Resolve<IAppStrikerRegistry>();
				_ = container.Resolve<IBattleSelectSetting>();
				_ = container.Resolve<IPlayerSelectSetting>();
				_ = container.Resolve<IAudioSetting>();
				_ = container.Resolve<IBattleRuleSetting>();
				_ = container.Resolve<IGamePadRegistry>();
				_ = container.Resolve<IAppTransitionFactory>();
				_ = container.Resolve<ICursorFactory>();
				_ = container.Resolve<IAppBGMPlayer>();
				_ = container.Resolve<ISceneLoader>();
				_ = container.Resolve<ISceneTransitionService>();
				_ = container.Resolve<ICursorDeployer>();
				container.Inject(sceneLoader);
				TryInjectSceneObjects(container, SceneManager.GetActiveScene());
			});
		}

		void TryInjectSceneObjects(IObjectResolver container, Scene scene) {
			if (!injectedSceneHandles.Add(scene.handle)) {
				return;
			}

			var rootObjects = scene.GetRootGameObjects();
			foreach (var root in rootObjects) {
				if (IsAnotherScopeRoot(root)) {
					continue;
				}

				container.InjectGameObject(root);
			}
		}

		bool IsAnotherScopeRoot(GameObject root) {
			if (!root.TryGetComponent<LifetimeScope>(out var rootScope)) {
				return false;
			}

			return rootScope != this;
		}

		sealed class SceneInjectionHandler : IInitializable, IDisposable {
			readonly IObjectResolver container;
			readonly AppScope appScope;

			public SceneInjectionHandler(IObjectResolver container, AppScope appScope) {
				this.container = container;
				this.appScope = appScope;
			}

			public void Initialize() {
				SceneManager.sceneLoaded += OnSceneLoaded;
			}

			public void Dispose() {
				SceneManager.sceneLoaded -= OnSceneLoaded;
			}

			void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
				appScope.TryInjectSceneObjects(container, scene);
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