
using System;
using System.Collections;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    
    public class BattleScope : LifetimeScope {
        const int MAX_CONTAINER_WAIT_FRAMES = 300;
        [SerializeField] BattleSetting battleSetting;
        [SerializeField] AudioSource audioSource;
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
                _ = container.Resolve<IBattleFlow>();
                _ = container.Resolve<IBattlePlayerPresenter[]>();
                _ = container.Resolve<IBeatjudge>();
                _ = container.Resolve<IMusicPlayer>();
                Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed.");
                Debug.Log($"{LOG_PREFIX} BuildCallback completed");
            });

            Debug.Log($"{LOG_PREFIX} Configure completed. scene={gameObject.scene.name}");
        }

        void Start() {
            Debug.Log($"{LOG_PREFIX} Start called. scene={gameObject.scene.name}, hasContainer={Container != null}");
            if (Container == null) {
                Debug.LogWarning($"{LOG_PREFIX} Start detected null container. Begin wait-and-retry startup.");
                StartCoroutine(WaitAndStartBattleFlow());
                return;
            }

            StartBattleFlow("Start");
        }

        IEnumerator WaitAndStartBattleFlow() {
            for (var frame = 0; frame < MAX_CONTAINER_WAIT_FRAMES; frame++) {
                if (Container != null) {
                    Debug.Log($"{LOG_PREFIX} Container became ready at frame={frame}. Starting battle flow.");
                    StartBattleFlow("WaitAndStartBattleFlow");
                    yield break;
                }

                if (frame % 30 == 0) {
                    Debug.LogWarning($"{LOG_PREFIX} Waiting container... frame={frame}");
                }

                yield return null;
            }

            Debug.LogError($"{LOG_PREFIX} Container did not become ready within {MAX_CONTAINER_WAIT_FRAMES} frames. BattleFlow startup failed.");
        }

        void StartBattleFlow(string source) {
            try {
                Debug.Log($"{LOG_PREFIX} {source} resolving IBattleFlow and invoking StartBattle");
                Container.Resolve<IBattleFlow>().StartBattle();
                Debug.Log($"{LOG_PREFIX} {source} invoked StartBattle");
            }
            catch (Exception exception) {
                Debug.LogError($"{LOG_PREFIX} {source} failed to start battle flow: {exception.Message}");
                Debug.LogException(exception);
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