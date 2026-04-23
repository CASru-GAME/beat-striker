
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Alice {
	[RequireComponent(typeof(AIRegistry))]
	[RequireComponent(typeof(AISetting))]
	[DefaultExecutionOrder(-10000)]
	public class AppScope : LifetimeScope {
		const string LOG_PREFIX = "[AppScope]";

		static AppScope instance;
		public static AppScope Instance => instance;

		[SerializeField] PlayerInputManager playerInputManager;
		[SerializeField] StageRegistry stageRegistry;
		[SerializeField] ScreenRegistry screenRegistry;
		[SerializeField] MusicRegistry musicRegistry;
		[SerializeField] AppStrikerRegistry strikerRegistry;
		[SerializeField] BattleSelectSetting battleSelectSetting;
		[SerializeField] PlayerSelectSetting playerSelectSetting;
		[SerializeField] TutorialSetting tutorialSetting;
		[SerializeField] AudioSetting audioSetting;
		[SerializeField] BattleRuleSetting battleRuleSetting;
		[SerializeField] AppTransitionFactory appTransitionFactory;
		[SerializeField] CursorFactory cursorFactory;
		[SerializeField] AppBGMPlayer appBgmPlayer;
		[SerializeField] AIRegistry aiRegistry;
		[SerializeField] AISetting aiSetting;

		protected override void Awake() {
			Debug.Log($"{LOG_PREFIX} Awake begin. scene={gameObject.scene.name}");
			aiRegistry = GetComponent<AIRegistry>();
			aiSetting = GetComponent<AISetting>();
			if (instance != null && instance != this) {
				Debug.LogWarning($"{LOG_PREFIX} Duplicate AppScope detected. existing={instance.name}, current={name}. current instance will be destroyed");
				Destroy(gameObject);
				return;
			}

			instance = this;
			DontDestroyOnLoad(gameObject);
			playerSelectSetting.InitializeDefaults();
			aiSetting.InitializeDefaults();
			base.Awake();
			Debug.Log($"{LOG_PREFIX} Awake completed. scene={gameObject.scene.name}");
		}

		protected override void OnDestroy() {
			Debug.Log($"{LOG_PREFIX} OnDestroy called. scene={gameObject.scene.name}");
			if (instance == this) {
				instance = null;
			}

			base.OnDestroy();
			Debug.Log($"{LOG_PREFIX} OnDestroy completed. scene={gameObject.scene.name}");
		}

		protected override void Configure(IContainerBuilder builder) {
			Debug.Log($"{LOG_PREFIX} Configure begin. scene={gameObject.scene.name}");
			builder.RegisterInstance<IStageRegistry>(stageRegistry);
			builder.RegisterInstance<IScreenRegistry>(screenRegistry);
			builder.RegisterInstance<IMusicRegistry>(musicRegistry);
			builder.RegisterInstance<IAppStrikerRegistry>(strikerRegistry);
			builder.RegisterInstance<IBattleSelectSetting>(battleSelectSetting);
			builder.RegisterInstance<IPlayerSelectSetting>(playerSelectSetting);
			builder.RegisterInstance<ITutorialSetting>(tutorialSetting);
			builder.RegisterInstance<IAudioSetting>(audioSetting);
			builder.Register<ICursorMoveSetting, CursorMoveSetting>(Lifetime.Singleton);
			builder.RegisterInstance<IBattleRuleSetting>(battleRuleSetting);
			builder.Register<IGamePadRegistry, GamePadRegistry>(Lifetime.Singleton);
			builder.RegisterInstance<IAppTransitionFactory>(appTransitionFactory);
			builder.RegisterInstance<ICursorFactory>(cursorFactory);
			builder.RegisterInstance<IAppBGMPlayer>(appBgmPlayer);
			builder.RegisterInstance<IAIRegistry>(aiRegistry);
			builder.RegisterInstance<IAISetting>(aiSetting);
			builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
			builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);

			builder.RegisterInstance(playerInputManager);
			builder.RegisterEntryPoint<CursorDeployer>(Lifetime.Singleton);

			builder.RegisterBuildCallback(container => {
				Debug.Log($"{LOG_PREFIX} BuildCallback begin. scene={gameObject.scene.name}");
				_ = container.Resolve<IStageRegistry>();
				_ = container.Resolve<IScreenRegistry>();
				_ = container.Resolve<IMusicRegistry>();
				_ = container.Resolve<IAppStrikerRegistry>();
				_ = container.Resolve<IBattleSelectSetting>();
				_ = container.Resolve<IPlayerSelectSetting>();
				_ = container.Resolve<ITutorialSetting>();
				_ = container.Resolve<IAudioSetting>();
				_ = container.Resolve<ICursorMoveSetting>();
				_ = container.Resolve<IBattleRuleSetting>();
				_ = container.Resolve<IGamePadRegistry>();
				_ = container.Resolve<IAppTransitionFactory>();
				_ = container.Resolve<ICursorFactory>();
				_ = container.Resolve<IAppBGMPlayer>();
				_ = container.Resolve<IAIRegistry>();
				_ = container.Resolve<IAISetting>();
				_ = container.Resolve<ISceneLoader>();
				_ = container.Resolve<ISceneTransitionService>();
				_ = container.Resolve<ICursorDeployer>();
				Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed");
			});
			Debug.Log($"{LOG_PREFIX} Configure completed. scene={gameObject.scene.name}");
		}

	}
}