
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    
    public class BattleScope : LifetimeScope {
        [SerializeField] BattleSetting battleSetting;
        [SerializeField] AudioSource audioSource;
        [SerializeField] BattlePresenterView battlePresenter;
        [SerializeField] ResultSceneView resultScene;
        [SerializeField] BattlePlayerView[] battlePlayerPresenters;

        protected override void Configure(IContainerBuilder builder) {
            builder.Register<IStrikerRegistry, StrikerRegistry>(Lifetime.Singleton);
            builder.Register<IStrikerFactory, StrikerHubFactory>(Lifetime.Singleton);
            builder.Register<IBattleDeployer, BattleDeployer>(Lifetime.Singleton);
            builder.Register<IBattleJudge, BattleJudge>(Lifetime.Singleton);
            builder.RegisterInstance(battlePresenter);
            builder.RegisterInstance(battlePresenter.SuspendMenuPresenter);
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
                _ = container.Resolve<IStrikerRegistry>();
                _ = container.Resolve<IStrikerFactory>();
                _ = container.Resolve<IBattleDeployer>();
                _ = container.Resolve<IBattleJudge>();
                _ = container.Resolve<IBattleSetting>();
                _ = container.Resolve<IAudioSetting>();
                _ = container.Resolve<IBattleSelectSetting>();
                _ = container.Resolve<IBattleFlow>();
                _ = container.Resolve<IBattlePlayerPresenter[]>();
                _ = container.Resolve<IBeatjudge>();
                _ = container.Resolve<IMusicPlayer>();

                InjectSceneObjects(container);
            });
        }

        void Start() {
            if (Container == null) {
                return;
            }

            Container.Resolve<IBattleFlow>().StartBattle();
        }

        void InjectSceneObjects(IObjectResolver container) {
            var rootObjects = gameObject.scene.GetRootGameObjects();
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

        sealed class BattleFlowStarter : IInitializable {
            readonly IBattleFlow battleFlow;

            public BattleFlowStarter(IBattleFlow battleFlow) {
                this.battleFlow = battleFlow;
            }

            public void Initialize() {
                battleFlow.StartBattle();
            }
        }

        sealed class BattlePlayerPresenterCollection : IDisposable {
            readonly BattlePlayerPresenter[] battlePlayerPresenters;

            public IBattlePlayerPresenter[] Presenters { get; }

            public BattlePlayerPresenterCollection(BattlePlayerView[] battlePlayerViews, IStrikerRegistry strikerRegistry, IBeatjudge beatJudge, IMusicPlayer musicPlayer) {
                battlePlayerPresenters = new BattlePlayerPresenter[battlePlayerViews.Length];
                var presenters = new IBattlePlayerPresenter[battlePlayerViews.Length];
                for (var i = 0; i < battlePlayerViews.Length; i++) {
                    var battlePlayerPresenter = new BattlePlayerPresenter(battlePlayerViews[i], strikerRegistry, beatJudge, musicPlayer);
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