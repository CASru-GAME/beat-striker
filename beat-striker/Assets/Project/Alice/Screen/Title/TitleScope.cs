using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class TitleScope : LifetimeScope {
        const string LOG_PREFIX = "[TitleScope]";

        [SerializeField] TitleScene titleScene;

        protected override void Configure(IContainerBuilder builder) {
            Debug.Log($"{LOG_PREFIX} Configure begin. scene={gameObject.scene.name}");
            builder.RegisterInstance(titleScene);
            builder.Register<TitlePresenter>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container => {
                Debug.Log($"{LOG_PREFIX} BuildCallback begin. scene={gameObject.scene.name}");
                _ = container.Resolve<TitlePresenter>();
                Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed. resolved={nameof(TitlePresenter)}");
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
