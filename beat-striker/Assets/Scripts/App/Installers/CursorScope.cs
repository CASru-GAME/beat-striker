using UnityEngine;
using VContainer;
using VContainer.Unity;

[RequireComponent(typeof(CursorView))]
public class CursorScope : LifetimeScope {
    protected override void Configure(IContainerBuilder builder) {
        var view = GetComponent<CursorView>();

        // ★CursorView を ICursorView としても解決可能にする
        //   (ICursorViewが解決できずCursorPresenterの生成に失敗していた問題)
        builder.RegisterComponent(view)
               .As<CursorView>()
               .As<ICursorView>(); // ★追加

        // CursorPresenterをICursorPresenterとして登録
        // IPlayerRegistry / IBus / ILife / PlayerId は親(AppFlowScope)から解決される想定
        builder.Register<CursorPresenter>(Lifetime.Scoped)
               .As<ICursorPresenter>();
    }
}
