using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class RankingScope : LifetimeScope {
        const string LOG_PREFIX = "[RankingScope]";

        [SerializeField] RankingPresenterView rankingPresenterView;
        [SerializeField] RankingHistoryListView rankingHistoryListView;

        protected override void Configure(IContainerBuilder builder) {
            Debug.Log($"{LOG_PREFIX} Configure begin. scene={gameObject.scene.name}");
            builder.RegisterInstance(rankingPresenterView);
            builder.RegisterInstance(rankingHistoryListView);
            builder.Register<RankingPresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => {
                Debug.Log($"{LOG_PREFIX} BuildCallback begin. scene={gameObject.scene.name}");
                _ = container.Resolve<RankingHistoryListView>();
                _ = container.Resolve<RankingPresenter>();
                Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed. resolved={nameof(RankingPresenter)}");
            });
            Debug.Log($"{LOG_PREFIX} Configure completed. scene={gameObject.scene.name}");
        }

        protected override LifetimeScope FindParent() {
            var parent = AppScope.Instance;
            Debug.Log($"{LOG_PREFIX} FindParent called. resolvedParent={parent != null}");
            return parent;
        }
    }
}
