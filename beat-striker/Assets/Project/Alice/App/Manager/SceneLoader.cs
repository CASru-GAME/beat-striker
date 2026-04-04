using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alice {
    public enum AppScene {
        Title,
        CharacterSelect,
        StageSelect,
        Battle,
        ResultMenu,
    }

    public interface ISceneLoader {
        Task LoadAsync(AppScene scene);
    }

    [Serializable]
    public class SceneLoaderEntry {
        public string SceneName;
        public AppScene Scene;
    }

    public class SceneLoader : MonoBehaviour, ISceneLoader {
        [SerializeField] SceneLoaderEntry[] sceneEntries;

        readonly Dictionary<AppScene, string> sceneNameMap = new();
        bool initialized;

        void EnsureInitialized() {
            if (initialized) {
                return;
            }

            sceneNameMap.Clear();
            for (var i = 0; i < sceneEntries.Length; i++) {
                var entry = sceneEntries[i];
                sceneNameMap[entry.Scene] = entry.SceneName;
            }

            initialized = true;
        }

        public async Task LoadAsync(AppScene scene) {
            EnsureInitialized();
            var asyncOperation = SceneManager.LoadSceneAsync(sceneNameMap[scene]);
            while (!asyncOperation.isDone) {
                await Task.Yield();
            }
        }
    }
}