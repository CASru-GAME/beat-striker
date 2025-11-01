using System;
using System.Collections.Generic;
using Core.App.Models;
using Core.App.Presenters.Scene;
using Core.App.Types;
using Core.App.Views;
using Core.App.Views.Scene;
using Core.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.App.Installers {

    [System.Serializable]
    public struct SceneNameEntry {
        public AppScene scene;
        public string sceneName;
    }

    [RequireComponent(typeof(SceneView))]
    [RequireComponent(typeof(Life))]
    [RequireComponent(typeof(BGMView))]
    public sealed class AppFlowScope : MonoBehaviour, ICursorFactory {
        private static AppFlowScope instance;
        
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
        IBGMManager bgmManager;

        void Awake() {
            // シングルトンパターン: 既にインスタンスが存在する場合は破棄
            if (instance != null && instance != this) {
                Debug.Log("AppFlowScope: Duplicate instance found, destroying this one");
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("AppFlowScope: Instance created and marked as DontDestroyOnLoad");

            life = GetComponent<Life>();
            var bus = this.GetBus();
            
            // BGMマネージャー初期化
            var bgmView = GetComponent<BGMView>();
            bgmManager = new BGMManager(bgmView, bus, life);
            
            playerRegistry = new PlayerRegistry(bus, life);
            cursorRegistry = new CursorRegistry(this, playerRegistry, bus, life);
            battleSettingModel = new BattleSettingModel(defaultStageId, defaultTrackId, defaultStrikerId);
            var sceneView = GetComponent<SceneView>();
            sceneView.Construct(CreateSceneNameDictFromEntries());
            
            // 現在のシーンから初期ステートを決定
            AppScene initialScene = DetermineInitialScene();
            Debug.Log($"AppFlowScope: Starting with scene {initialScene}");
            
            // Lifeを有効化してからPresenterを作成（これによりBGMManagerのSubscribeが先に実行される）
            life.SetEnable(true);
            var presenter = new SceneStatePresenter(initialScene, sceneView, bus, battleSettingModel, this, cursorRegistry, life);

        }
        
        AppScene DetermineInitialScene() {
            // 現在ロードされているシーン名を取得
            string currentSceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"AppFlowScope: Current scene name is '{currentSceneName}'");
            
            // シーン名からAppSceneを逆引き
            foreach (var entry in sceneNameEntries) {
                if (entry.sceneName == currentSceneName) {
                    Debug.Log($"AppFlowScope: Matched to AppScene.{entry.scene}");
                    return entry.scene;
                }
            }
            
            // マッチしない場合はfirstSceneを使用
            Debug.Log($"AppFlowScope: No match found, using default firstScene: {firstScene}");
            return firstScene;
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