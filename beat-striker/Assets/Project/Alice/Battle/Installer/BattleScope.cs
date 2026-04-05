
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    
    public class BattleScope : LifetimeScope {
        [SerializeField] BattleSetting battleSetting;
        [SerializeField] AudioSource audioSource;
        [SerializeField] BattlePresenter battlePresenter;
        [SerializeField] BattlePlayerPresenter[] battlePlayerPresenters;

        protected override void Configure(IContainerBuilder builder) {
            builder.Register<IStrikerRegistry, StrikerRegistry>(Lifetime.Singleton);
            builder.Register<IStrikerFactory, StrikerHubFactory>(Lifetime.Singleton);
            builder.Register<IBattleDeployer, BattleDeployer>(Lifetime.Singleton);
            builder.Register<IBattleJudge, BattleJudge>(Lifetime.Singleton);
            var battlePlayerPresenters = new IBattlePlayerPresenter[this.battlePlayerPresenters.Length];
            for (var i = 0; i < this.battlePlayerPresenters.Length; i++) {
                battlePlayerPresenters[i] = this.battlePlayerPresenters[i];
            }
            builder.RegisterInstance<IBattlePresenter>(battlePresenter);
            builder.RegisterInstance(battlePlayerPresenters);
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

                container.Inject(battlePresenter);
                for (var i = 0; i < this.battlePlayerPresenters.Length; i++) {
                    container.Inject(this.battlePlayerPresenters[i]);
                }

                InjectSceneObjects(container);
            });
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

    }
}