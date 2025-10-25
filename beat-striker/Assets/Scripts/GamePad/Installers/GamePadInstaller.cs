using Core.GamePad.Models;
using Core.GamePad.Presenters;
using Core.GamePad.Types;
using Core.GamePad.Views;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class GamePadScope : LifetimeScope
{
    [SerializeField] GamePad view;
    [SerializeField] int idSeed = 0;

    protected override void Configure(IContainerBuilder b)
    {
        // ViewをComponentとして登録
        b.RegisterComponent(view);  // .AsSelf() が暗黙的に付きます

        // Model登録（インスペクタ値を渡す）
        b.Register<GamePadModel>(Lifetime.Scoped)
         .WithParameter("id", new GamePadId(idSeed))
         .WithParameter("config", view.Config);

        // Presenter登録
        b.Register<GamePadPresenter>(Lifetime.Scoped);
    }
}
