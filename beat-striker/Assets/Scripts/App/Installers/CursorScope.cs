using UnityEngine;
using VContainer;
using VContainer.Unity;

public class CursorScope : LifetimeScope {
    protected override void Configure(IContainerBuilder builder) {
        builder.Register<CursorPresenter>(Lifetime.Scoped).As<ICursorPresenter>();
        builder.Register<CursorView>(Lifetime.Scoped).As<ICursorView>();
    }
}
