

using System;
using Core.App.Models;
using Core.App.Presenters.Scene;
using Core.App.Types;
using Core.App.Views.Scene;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.App.Installers {

    [RequireComponent(typeof(SceneView))]
    [RequireComponent(typeof(Life))]
    public sealed class SceneFlowScope : LifetimeScope, ICursorFactory, ILife {
        [SerializeField] CursorScope cursorPrefab;
        Life life;

        protected override void Configure(IContainerBuilder builder) {
            life = GetComponent<Life>();
            builder.RegisterInstance(life).As<ILife>();

            builder.Register<PlayerRegistry>(Lifetime.Scoped).As<IPlayerRegistry>();

            var manager = GetComponent<SceneView>();
            builder.RegisterComponent(manager).As<ISceneView>();

            builder.Register<BattleSettingModel>(Lifetime.Scoped).As<IBattleSettingModel>();
            builder.Register<SceneStatePresenter>(Lifetime.Scoped).As<ISceneStateController>().As<ISceneStateFactory>();
            builder.Register<SceneStateContext>(Lifetime.Scoped).As<SceneStateContext>();
        }

        public void CreateCursor(PlayerId id) {
            var cursorScopeInstance = CreateChildFromPrefab(
                cursorPrefab,
                builder => {
                    builder.RegisterInstance(id).As<PlayerId>();
                }
            );
        }

        public void Link(Action onEnabled, Action onDisabled) {
            throw new NotImplementedException();
        }

        public void Unlink(Action onEnabled, Action onDisabled) {
            throw new NotImplementedException();
        }
    }
}