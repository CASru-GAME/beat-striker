using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class BattleTransitionScope : LifetimeScope {
        const string LOG_PREFIX = "[BattleTransitionScope]";

        [SerializeField] BattleTransitionView battleTransitionView;

        protected override void Configure(IContainerBuilder builder) {
            Debug.Log($"{LOG_PREFIX} Configure begin. scene={gameObject.scene.name}");
            builder.RegisterInstance(battleTransitionView);
            builder.Register<BattleTransitionPresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => {
                _ = container.Resolve<BattleTransitionPresenter>();
                Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed. resolved={nameof(BattleTransitionPresenter)}");
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
