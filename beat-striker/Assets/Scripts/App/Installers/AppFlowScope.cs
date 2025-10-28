

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
    public sealed class AppFlowScope : MonoBehaviour, ICursorFactory {
        [SerializeField] Canvas canvas;
        [SerializeField] CursorScope cursorPrefab;
        [SerializeField] SceneNameEntry[] sceneNameEntries;
        [SerializeField] AppScene firstScene;
        Life life;

        ICursorRegistry cursorRegistry;
        IPlayerRegistry playerRegistry;

        void Awake() {
            Debug.Log("AppFlowScope Configure");

            life = GetComponent<Life>();
            var bus = this.GetBus();
            playerRegistry = new PlayerRegistry(bus, life);
            cursorRegistry = new CursorRegistry(this, playerRegistry, bus, life);
            var bm = new BattleSettingModel();
            var sceneView = GetComponent<SceneView>();
            sceneView.Construct(CreateSceneNameDictFromEntries());
            var presenter = new SceneStatePresenter(firstScene,sceneView,bus,bm,this,cursorRegistry,life);
        }

        Dictionary<AppScene, string> CreateSceneNameDictFromEntries() {
            var dict = new Dictionary<AppScene, string>();
            foreach (var entry in sceneNameEntries) {
                dict[entry.scene] = entry.sceneName;
            }
            return dict;
        }


        public void CreateCursor(PlayerId id) {
            var cursor = Instantiate(cursorPrefab,canvas.transform);
            cursor.Construct(id, playerRegistry);
        }
    }
}