
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    
    [RequireComponent(typeof(BattleConfig))]
    [RequireComponent(typeof(BeatConfig))]
    [RequireComponent(typeof(AudioSource))]
    public class BattleScope : LifetimeScope {
        [SerializeField] BattlePresenter battlePresenter;
        [SerializeField] BattlePlayerPresenter[] battlePlayerPresenters;

        protected override void Configure(IContainerBuilder builder) {
            builder.Register<IStrikerRegistry, StrikerRegistry>(Lifetime.Singleton);
            builder.Register<IStrikerFactory, StrikerHubFactory>(Lifetime.Singleton);
            builder.Register<IBattleDeployer, BattleDeployer>(Lifetime.Singleton);
            builder.Register<IBattleJudge, BattleJudge>(Lifetime.Singleton);
            builder.Register<IBattleAppSelectionApplier, BattleAppSelectionApplier>(Lifetime.Singleton);
            builder.RegisterInstance<IBattlePresenter>(battlePresenter);
            var battlePlayerPresenters = new IBattlePlayerPresenter[this.battlePlayerPresenters.Length];
            for (var i = 0; i < this.battlePlayerPresenters.Length; i++) {
                battlePlayerPresenters[i] = this.battlePlayerPresenters[i];
            }
            builder.RegisterInstance(battlePlayerPresenters);
            builder.Register<IBattleFlow, BattleFlow>(Lifetime.Singleton);
            builder.Register<IBeatjudge, BeatJudge>(Lifetime.Singleton);
            builder.Register<IMusicPlayer, MusicPlayer>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BattleFlowStarter>(Lifetime.Singleton);

            builder.RegisterInstance(GetComponent<BattleConfig>());
            builder.RegisterInstance(GetComponent<BeatConfig>());
            builder.RegisterInstance(GetComponent<AudioSource>());

            builder.RegisterBuildCallback(container => {
                _ = container.Resolve<IStrikerRegistry>();
                _ = container.Resolve<IStrikerFactory>();
                _ = container.Resolve<IBattleDeployer>();
                _ = container.Resolve<IBattleJudge>();
                _ = container.Resolve<IBattleAppSelectionApplier>();
                _ = container.Resolve<IBattleFlow>();
                _ = container.Resolve<IBattlePlayerPresenter[]>();
                _ = container.Resolve<IBeatjudge>();
                _ = container.Resolve<IMusicPlayer>();
                InjectSceneObjects(container);
            });
        }

        void InjectSceneObjects(IObjectResolver container) {
            var rootObjects = gameObject.scene.GetRootGameObjects();
            foreach (var root in rootObjects) {
                container.InjectGameObject(root);
            }
        }

        sealed class BattleFlowStarter : IInitializable {
            readonly IBattleFlow battleFlow;
            readonly IBattleAppSelectionApplier appSelectionApplier;

            public BattleFlowStarter(IBattleFlow battleFlow, IBattleAppSelectionApplier appSelectionApplier) {
                this.battleFlow = battleFlow;
                this.appSelectionApplier = appSelectionApplier;
            }

            public void Initialize() {
                appSelectionApplier.Apply();
                battleFlow.StartBattle();
            }
        }

    }
}