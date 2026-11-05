using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class OnlineRoomScope : LifetimeScope {
        [SerializeField] OnlineRoomView view;

        [SerializeField] FocusGraphicRaycaster[] focusRaycasters;
        [SerializeField] InputFocusBoundary inputFocusBoundary;
        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterBuildCallback(container => {
                var focus = container.Resolve<IInputFocusService>();
                foreach (var raycaster in focusRaycasters) raycaster.Initialize(focus);
            });
            builder.RegisterComponent(inputFocusBoundary);
            builder.RegisterInstance(view);
            builder.Register<OnlineRoomPresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => container.Resolve<OnlineRoomPresenter>());
        }

        protected override LifetimeScope FindParent() => AppScope.Instance;
    }
}
