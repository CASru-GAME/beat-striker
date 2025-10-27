

using System;
using System.Collections;
using System.Collections.Generic;
using Core.App.Presenters.Scene;
using Core.App.Types;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Core.App.Views.Scene {
    public class SceneView : MonoBehaviour, ISceneView {
        private Dictionary<AppScene, string> sceneNames = new();
        private IPlayerRegistry playerRegistry;


        [Inject]
        public void Construct(Dictionary<AppScene, string> sceneNames, IPlayerRegistry playerRegistry) {
            this.sceneNames = sceneNames;
            this.playerRegistry = playerRegistry;
        }

        public void LoadScene(AppScene scene, Action<AppScene> OnSceneLoadCompleted) {
            if (!sceneNames.ContainsKey(scene)) {
                Debug.LogError($"Scene '{scene}' not found in sceneNames dictionary.");
                return;
            }

            StartCoroutine(LoadSceneAsyncCoroutine(scene, OnSceneLoadCompleted));
        }

        private IEnumerator LoadSceneAsyncCoroutine(AppScene scene, Action<AppScene> OnSceneLoadCompleted) {
            if (!sceneNames.ContainsKey(scene)) {
                Debug.LogError($"Scene '{scene}' not found in sceneNames dictionary.");
                yield break;
            }

            var sceneName = sceneNames[scene];
            yield return SceneManager.LoadSceneAsync(sceneName);
            OnSceneLoadCompleted?.Invoke(scene);
        }
    }
}