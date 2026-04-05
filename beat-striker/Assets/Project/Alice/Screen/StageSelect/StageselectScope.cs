using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class StageselectScope : LifetimeScope {
        const string LOG_PREFIX = "[StageselectScope]";

        [SerializeField] StageselectScene stageSelectScene;

        protected override void Configure(IContainerBuilder builder) {
            Debug.Log($"{LOG_PREFIX} Configure begin. scene={gameObject.scene.name}");
            builder.RegisterInstance(stageSelectScene);
            builder.Register<StageselectPresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => {
                Debug.Log($"{LOG_PREFIX} BuildCallback begin. scene={gameObject.scene.name}");
                _ = container.Resolve<StageselectPresenter>();
                Debug.Log($"{LOG_PREFIX} BuildCallback resolve completed. resolved={nameof(StageselectPresenter)}");
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
