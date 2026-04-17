using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class BackSceneScope : LifetimeScope {
        const string LOG_PREFIX = "[BackSceneScope]";

        [SerializeField] BackSelectSceneTextHover[] views;

        protected override void Configure(IContainerBuilder builder) {
            Debug.Log($"{LOG_PREFIX} Configure begin. scene={gameObject.scene.name}, viewCount={views.Length}");
            builder.RegisterInstance(views);
            builder.Register<BackScenePresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => {
                Debug.Log($"{LOG_PREFIX} BuildCallback begin. scene={gameObject.scene.name}");
                _ = container.Resolve<BackScenePresenter>();
                Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed. resolved={nameof(BackScenePresenter)}");
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
