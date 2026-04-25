using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class BootScope : LifetimeScope {
        const string LOG_PREFIX = "[BootScope]";

        [SerializeField] BootScene bootScene;

        protected override void Configure(IContainerBuilder builder) {
            Debug.Log($"{LOG_PREFIX} Configure begin. scene={gameObject.scene.name}");
            builder.RegisterInstance(bootScene);
            builder.Register<BootPresenter>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container => {
                Debug.Log($"{LOG_PREFIX} BuildCallback begin. scene={gameObject.scene.name}");
                _ = container.Resolve<BootPresenter>();
                Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed. resolved={nameof(BootPresenter)}");
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
