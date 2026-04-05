using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class BackSceneScope : LifetimeScope {
        [SerializeField] BackSelectSceneTextHover[] views;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterInstance(views);
            builder.Register<BackScenePresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => {
                _ = container.Resolve<BackScenePresenter>();
            });
        }
    }
}
