using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Alice {
	[RequireComponent(typeof(AIRegistry))]
	[RequireComponent(typeof(AISetting))]
	[RequireComponent(typeof(AppNetworkSetting))]
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
		[SerializeField] AppAudioPlayer appAudioPlayer;
		[SerializeField] AIRegistry aiRegistry;
		[SerializeField] AISetting aiSetting;
		[SerializeField] AppUISetting appUiSetting;
		[SerializeField] AppNetworkSetting appNetworkSetting;
		[SerializeField] VirtualTouchControllerCanvasView virtualTouchControllerCanvasView;
		[SerializeField] LoadingView loadingView;
		[SerializeField] AppOverlayView appOverlayView;

		public IAppAudioPlayer AppAudioPlayer => appAudioPlayer;

		protected override void Awake() {
			Debug.Log($"{LOG_PREFIX} Awake begin. scene={gameObject.scene.name}");
			aiRegistry = GetComponent<AIRegistry>();
			aiSetting = GetComponent<AISetting>();
			appNetworkSetting = GetComponent<AppNetworkSetting>();
			if (instance != null && instance != this) {
				Debug.LogWarning($"{LOG_PREFIX} Duplicate AppScope detected. existing={instance.name}, current={name}. current instance will be destroyed. Remove the extra App/LifetimeScope root from the loaded scene so only the DontDestroyOnLoad App remains.");
				Destroy(gameObject);
				return;
			}

			instance = this;
			DontDestroyOnLoad(gameObject);
			appAudioPlayer = GetComponent<AppAudioPlayer>();
			if (!appAudioPlayer) {
				appAudioPlayer = gameObject.AddComponent<AppAudioPlayer>();
			}
			appAudioPlayer.Initialize(audioSetting);
			playerSelectSetting.InitializeDefaults();
			aiSetting.InitializeDefaults();
			appUiSetting.InitializeDefaults();
			appNetworkSetting.InitializeDefaults();
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
			builder.RegisterComponent(musicRegistry).As<IMusicRegistry>();
			builder.RegisterComponent(strikerRegistry).As<IAppStrikerRegistry>();
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
			builder.RegisterInstance<IAppAudioPlayer>(appAudioPlayer);
			builder.RegisterInstance<IAIRegistry>(aiRegistry);
			builder.RegisterInstance<IAISetting>(aiSetting);
			builder.RegisterInstance<IAppUISetting>(appUiSetting);
			builder.RegisterInstance<IAppNetworkSetting>(appNetworkSetting);
			builder.RegisterInstance(virtualTouchControllerCanvasView);
			builder.RegisterComponent(loadingView);
			builder.RegisterComponent(appOverlayView);
			builder.Register<ILoadingOverlayService, LoadingOverlayService>(Lifetime.Singleton);
			builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
			builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);
			builder.Register<IOnlineDuelIdentity, OnlineDuelIdentity>(Lifetime.Singleton);
			builder.Register<IOnlineDuelCoordinator, OnlineDuelCoordinator>(Lifetime.Singleton);
			builder.Register<IBattleHistoryApiClient, BattleHistoryApiClient>(Lifetime.Singleton);
			builder.Register<IReplaySetting, ReplaySetting>(Lifetime.Singleton);

			builder.RegisterInstance(playerInputManager);
			builder.RegisterEntryPoint<CursorDeployer>(Lifetime.Singleton);
			builder.RegisterEntryPoint<VirtualTouchControllerPresenter>(Lifetime.Singleton);
			builder.RegisterEntryPoint<OnlineDuelFusionClient>(Lifetime.Singleton);
			builder.RegisterEntryPoint<AppOverlayPresenter>(Lifetime.Singleton);

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
				_ = container.Resolve<IAppAudioPlayer>();
				_ = container.Resolve<IAIRegistry>();
				_ = container.Resolve<IAISetting>();
				_ = container.Resolve<IAppUISetting>();
				_ = container.Resolve<IAppNetworkSetting>();
				_ = container.Resolve<VirtualTouchControllerCanvasView>();
				_ = container.Resolve<ILoadingOverlayService>();
				_ = container.Resolve<ISceneLoader>();
				_ = container.Resolve<ISceneTransitionService>();
				_ = container.Resolve<IOnlineDuelIdentity>();
				_ = container.Resolve<IOnlineDuelFusionClient>();
				_ = container.Resolve<IOnlineSessionBootstrap>();
				_ = container.Resolve<INetworkRunnerProvider>();
				_ = container.Resolve<IOnlineDuelCoordinator>();
				_ = container.Resolve<IAppOverlayPresenter>();
				_ = container.Resolve<IBattleHistoryApiClient>();
				_ = container.Resolve<IReplaySetting>();
				_ = container.Resolve<ICursorDeployer>();
				Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed");
			});
			Debug.Log($"{LOG_PREFIX} Configure completed. scene={gameObject.scene.name}");
		}

	}
}
