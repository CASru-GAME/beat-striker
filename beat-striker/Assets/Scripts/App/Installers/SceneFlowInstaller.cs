

using Core.App.Models;
using Core.App.Presenters.Scene;
using Core.App.Views.Scene;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.App.Installers {

    [RequireComponent(typeof(SceneView))]
    public sealed class SceneFlowInstaller : LifetimeScope {

        protected override void Configure(IContainerBuilder builder) {

            var view = GetComponent<SceneView>();
            builder.RegisterComponent(view).As<ISceneView>();

            builder.Register<BattleSettingModel>(Lifetime.Scoped).As<IBattleSettingModel>();
            builder.Register<SceneStatePresenter>(Lifetime.Scoped).As<ISceneStateController>().As<ISceneStateFactory>();
            builder.Register<SceneStateContext>(Lifetime.Scoped).As<SceneStateContext>();
        }
    }
}