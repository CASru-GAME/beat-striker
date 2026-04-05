
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Alice {
	public class AppScope : LifetimeScope {
		static AppScope instance;

		[SerializeField] PlayerInputManager playerInputManager;
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
			builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
			builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);

			builder.RegisterInstance(playerInputManager);
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
			});
		}

	}
}