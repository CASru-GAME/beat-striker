using VContainer;
using VContainer.Unity;

namespace App {
    public class AppScope : LifetimeScope {
        protected override void Configure(IContainerBuilder builder) {
            builder.Register<ISceneFlow, SceneFlow>(Lifetime.Singleton);
        }
    }
}