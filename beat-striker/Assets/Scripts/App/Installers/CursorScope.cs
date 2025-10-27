using UnityEngine;
using VContainer;
using VContainer.Unity;

[RequireComponent(typeof(CursorView))]
public class CursorScope : LifetimeScope {
    protected override void Configure(IContainerBuilder builder) {
        var view = GetComponent<CursorView>();

        builder.RegisterComponent(view)
               .As<CursorView>()
               .As<ICursorView>();

        builder.Register<CursorPresenter>(Lifetime.Scoped)
               .As<ICursorPresenter>();
    }
}
