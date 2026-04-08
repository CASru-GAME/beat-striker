
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    
    public class BattleScope : LifetimeScope {
        [SerializeField] BattleSetting battleSetting;
        [SerializeField] AudioSource audioSource;
        [SerializeField] BattleOpeningBgmPlayer battleOpeningBgmPlayer;
        [SerializeField] BattlePresenterView battlePresenter;
        [SerializeField] ResultSceneView resultScene;
        [SerializeField] BattlePlayerView[] battlePlayerPresenters;

        const string LOG_PREFIX = "[BattleScope]";

        protected override void Configure(IContainerBuilder builder) {
            Debug.Log($"{LOG_PREFIX} Configure begin. scene={gameObject.scene.name}, playerPresenterCount={battlePlayerPresenters.Length}");
            builder.Register<IStrikerRegistry, StrikerRegistry>(Lifetime.Singleton);
            builder.Register<IStrikerFactory, StrikerHubFactory>(Lifetime.Singleton);
            builder.Register<IBattleDeployer, BattleDeployer>(Lifetime.Singleton);
            builder.Register<IBattleJudge, BattleJudge>(Lifetime.Singleton);
            builder.RegisterInstance(battlePresenter);
            builder.RegisterInstance(battlePresenter.SuspendMenuPresenter);
            builder.RegisterInstance<IBattleOpeningBgmPlayer>(battleOpeningBgmPlayer);
            builder.RegisterInstance(battlePlayerPresenters);
            builder.Register<BattleSuspendMenuPresenter>(Lifetime.Singleton);
            builder.Register<IBattlePresenter, BattlePresenter>(Lifetime.Singleton);
            builder.Register<BattlePlayerPresenterCollection>(Lifetime.Singleton);
            builder.Register<IBattlePlayerPresenter[]>(resolver => resolver.Resolve<BattlePlayerPresenterCollection>().Presenters, Lifetime.Singleton);
            builder.RegisterInstance(resultScene);
            builder.Register<ResultScene>(Lifetime.Singleton);
            builder.Register<IBattleFlow, BattleFlow>(Lifetime.Singleton);
            builder.Register<IBeatjudge, BeatJudge>(Lifetime.Singleton);
            builder.Register<IMusicPlayer, MusicPlayer>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BattleFlowStarter>(Lifetime.Singleton);

            builder.RegisterInstance<IBattleSetting>(battleSetting);
            builder.RegisterInstance(audioSource);

            builder.RegisterBuildCallback(container => {
                Debug.Log($"{LOG_PREFIX} BuildCallback begin. scene={gameObject.scene.name}");
                _ = container.Resolve<IStrikerRegistry>();
                _ = container.Resolve<IStrikerFactory>();
                _ = container.Resolve<IBattleDeployer>();
                _ = container.Resolve<IBattleJudge>();
                _ = container.Resolve<IBattleSetting>();
                _ = container.Resolve<IAudioSetting>();
                _ = container.Resolve<IBattleSelectSetting>();
                _ = container.Resolve<IBattleOpeningBgmPlayer>();
                _ = container.Resolve<IBattleFlow>();
                _ = container.Resolve<IBattlePlayerPresenter[]>();
                _ = container.Resolve<IBeatjudge>();
                _ = container.Resolve<IMusicPlayer>();
                Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed.");
                Debug.Log($"{LOG_PREFIX} BuildCallback completed");
            });

            Debug.Log($"{LOG_PREFIX} Configure completed. scene={gameObject.scene.name}");
        }

        protected override LifetimeScope FindParent() {
            var parent = AppScope.Instance;
            Debug.Log($"{LOG_PREFIX} FindParent called. resolvedParent={parent != null}");
            return parent;
        }

        void Start() {
            Debug.Log($"{LOG_PREFIX} Start called. scene={gameObject.scene.name}, hasContainer={Container != null}");
            if (Container == null) {
                Debug.LogError($"{LOG_PREFIX} Start detected null container after Awake build. parent resolution may have failed.");
            }
        }

        sealed class BattleFlowStarter : IInitializable {
            readonly IBattleFlow battleFlow;

            public BattleFlowStarter(IBattleFlow battleFlow) {
                this.battleFlow = battleFlow;
            }

            public void Initialize() {
                Debug.Log($"{LOG_PREFIX} BattleFlowStarter.Initialize invoke StartBattle");
                battleFlow.StartBattle();
                Debug.Log($"{LOG_PREFIX} BattleFlowStarter.Initialize completed");
            }
        }

        sealed class BattlePlayerPresenterCollection : IDisposable {
            readonly BattlePlayerPresenter[] battlePlayerPresenters;

            public IBattlePlayerPresenter[] Presenters { get; }

            public BattlePlayerPresenterCollection(BattlePlayerView[] battlePlayerViews, IStrikerRegistry strikerRegistry, IBeatjudge beatJudge, IMusicPlayer musicPlayer, IBattlePresenter battlePresenter, IPlayerSelectSetting playerSelectSetting, IAppStrikerRegistry appStrikerRegistry) {
                battlePlayerPresenters = new BattlePlayerPresenter[battlePlayerViews.Length];
                var presenters = new IBattlePlayerPresenter[battlePlayerViews.Length];
                for (var i = 0; i < battlePlayerViews.Length; i++) {
                    var battlePlayerPresenter = new BattlePlayerPresenter(battlePlayerViews[i], strikerRegistry, beatJudge, musicPlayer, battlePresenter.OnAttentionActiveStateChanged, playerSelectSetting, appStrikerRegistry);
                    battlePlayerPresenters[i] = battlePlayerPresenter;
                    presenters[i] = battlePlayerPresenter;
                }

                Presenters = presenters;
            }

            public void Dispose() {
                for (var i = 0; i < battlePlayerPresenters.Length; i++) {
                    battlePlayerPresenters[i].Dispose();
                }
            }
        }

    }
}