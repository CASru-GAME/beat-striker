

using System;
using System.Collections;
using System.Collections.Generic;
using Core.App.Presenters.Scene;
using Core.App.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.App.Views.Scene {
    public class SceneView : MonoBehaviour, ISceneView {
        private Dictionary<FAFA, string> sceneNames = new();

        public void Construct(Dictionary<FAFA, string> sceneNames) {
            this.sceneNames = sceneNames;
        }

        public void LoadScene(FAFA scene, Action<FAFA> OnSceneLoadCompleted) {
            if (!sceneNames.ContainsKey(scene)) {
                Debug.LogError($"Scene '{scene}' not found in sceneNames dictionary.");
                return;
            }

            StartCoroutine(LoadSceneAsyncCoroutine(scene, OnSceneLoadCompleted));
        }

        private IEnumerator LoadSceneAsyncCoroutine(FAFA scene, Action<FAFA> OnSceneLoadCompleted) {
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