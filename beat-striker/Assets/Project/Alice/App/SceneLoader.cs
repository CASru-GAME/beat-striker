using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alice {

    public interface ISceneLoader {
        Task LoadAsync(AppScene scene);
    }


    public class SceneLoader : ISceneLoader {
        readonly IReadOnlyDictionary<AppScene, string> sceneNameMap;

        public SceneLoader(IReadOnlyDictionary<AppScene, string> sceneNameMap) {
            this.sceneNameMap = sceneNameMap;
        }

        public async Task LoadAsync(AppScene scene) {
            var asyncOperation = SceneManager.LoadSceneAsync(sceneNameMap[scene]);
            while (!asyncOperation.isDone) {
                await Task.Yield();
            }
        }
    }
}