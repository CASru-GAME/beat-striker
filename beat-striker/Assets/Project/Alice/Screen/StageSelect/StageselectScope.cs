using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class StageselectScope : LifetimeScope {
        [SerializeField] StageselectScene stageSelectScene;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterInstance(stageSelectScene);
            builder.Register<StageselectPresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => {
                _ = container.Resolve<StageselectPresenter>();
            });
        }
    }
}
