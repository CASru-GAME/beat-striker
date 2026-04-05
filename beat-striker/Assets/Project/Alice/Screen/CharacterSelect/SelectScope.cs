using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class SelectScope : LifetimeScope {
        [SerializeField] SelectScene selectScene;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterInstance(selectScene);
            builder.Register<SelectPresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => {
                _ = container.Resolve<SelectPresenter>();
            });
        }
    }
}
