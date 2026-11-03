using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class OnlineRoomScope : LifetimeScope {
        [SerializeField] OnlineRoomView view;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterInstance(view);
            builder.Register<OnlineRoomPresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => container.Resolve<OnlineRoomPresenter>());
        }

        protected override LifetimeScope FindParent() => AppScope.Instance;
    }
}
