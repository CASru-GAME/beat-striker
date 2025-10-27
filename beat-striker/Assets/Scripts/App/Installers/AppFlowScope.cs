

using System;
using System.Collections.Generic;
using Core.App.Models;
using Core.App.Presenters.Scene;
using Core.App.Types;
using Core.App.Views.Scene;
using Core.Utils;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.App.Installers {

    [System.Serializable]
    public struct SceneNameEntry {
        public AppScene scene;
        public string sceneName;
    }

    [RequireComponent(typeof(SceneView))]
    [RequireComponent(typeof(Life))]
    public sealed class AppFlowScope : LifetimeScope, ICursorFactory {
        [SerializeField] CursorScope cursorPrefab;
        [SerializeField] SceneNameEntry[] sceneNameEntries;
        Life life;

        protected override void Configure(IContainerBuilder builder) {

            life = GetComponent<Life>();
            builder.RegisterInstance(life).As<ILife>();

            builder.RegisterInstance(this).As<ICursorFactory>();

            builder.Register<PlayerRegistry>(Lifetime.Scoped)
                   .As<IPlayerRegistry>();

            builder.Register<CursorRegistry>(Lifetime.Scoped)
                   .As<ICursorRegistry>();

            builder.Register<BattleSettingModel>(Lifetime.Scoped)
                   .As<IBattleSettingModel>();


            var bus = new Bus();
            builder.RegisterInstance(bus).As<IBus>();

            var sceneView = GetComponent<SceneView>();
            builder.RegisterComponent(sceneView).As<ISceneView>();

            builder.RegisterInstance(CreateSceneNameDictFromEntries())
                   .As<Dictionary<AppScene, string>>();

            builder.Register<SceneStatePresenter>(Lifetime.Scoped)
                   .As<ISceneStateController>()
                   .As<ISceneStateFactory>();

            builder.Register<SceneStateContext>(Lifetime.Scoped);
        }


        Dictionary<AppScene, string> CreateSceneNameDictFromEntries() {
            var dict = new Dictionary<AppScene, string>();
            foreach (var entry in sceneNameEntries) {
                dict[entry.scene] = entry.sceneName;
            }
            return dict;
        }

        public void CreateCursor(PlayerId id) {

            CreateChildFromPrefab(
                cursorPrefab,
                builder => {
                    builder.RegisterInstance(id).As<PlayerId>();
                }
            );
        }
    }
}