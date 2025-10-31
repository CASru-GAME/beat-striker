

using System;
using System.Collections.Generic;
using Core.App.Models;
using Core.App.Presenters.Scene;
using Core.App.Types;
using Core.App.Views.Scene;
using Core.Utils;
using UnityEngine;

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
        [SerializeField] StrikerId defaultStrikerId;
        [SerializeField] StageId defaultStageId;
        [SerializeField] TrackId defaultTrackId;
        Life life;

        ICursorRegistry cursorRegistry;
        public IPlayerRegistry playerRegistry;
        public IBattleSettingModel battleSettingModel;

        void Awake() {
            Debug.Log("AppFlowScope Configure");

            life = GetComponent<Life>();
            var bus = this.GetBus();
            playerRegistry = new PlayerRegistry(bus, life);
            cursorRegistry = new CursorRegistry(this, playerRegistry, bus, life);
            battleSettingModel = new BattleSettingModel(defaultStageId, defaultTrackId, defaultStrikerId);
            var sceneView = GetComponent<SceneView>();
            sceneView.Construct(CreateSceneNameDictFromEntries());
            var presenter = new SceneStatePresenter(firstScene, sceneView, bus, battleSettingModel, this, cursorRegistry, life);

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