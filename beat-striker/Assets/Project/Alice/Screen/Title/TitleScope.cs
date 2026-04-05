using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class TitleScope : LifetimeScope {
        [SerializeField] TitleScene titleScene;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterInstance(titleScene);
            builder.Register<TitlePresenter>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container => {
                _ = container.Resolve<TitlePresenter>();
            });
        }
    }
}
