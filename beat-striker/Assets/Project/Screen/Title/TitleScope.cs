using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class TitleScope : LifetimeScope {
        const string LOG_PREFIX = "[TitleScope]";

        [SerializeField] TitleScene titleScene;

        [SerializeField] FocusGraphicRaycaster[] focusRaycasters;
        [SerializeField] InputFocusBoundary inputFocusBoundary;
        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterBuildCallback(container => {
                var focus = container.Resolve<IInputFocusService>();
                foreach (var raycaster in focusRaycasters) raycaster.Initialize(focus);
            });
            builder.RegisterComponent(inputFocusBoundary);
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
