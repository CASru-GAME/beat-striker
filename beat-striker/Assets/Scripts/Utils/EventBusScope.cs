using VContainer;
using VContainer.Unity;
using Core.Utils;

namespace Core.Utils {

    /// <summary>
    /// イベントバスのスコープ管理クラス
    /// </summary>
    public class EventBusScope : LifetimeScope {

        protected override void Configure(IContainerBuilder builder) {
            builder.Register<Bus>(Lifetime.Singleton).As<IBus>();
        }

    }
}