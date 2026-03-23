using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace App {
    public class SceneLoader : MonoBehaviour, ISceneLoader {

        void Awake() {
        }

        void ISceneLoader.LoadScene(SceneTransitionRequest scene, System.Action onComplete) {
            StartCoroutine(LoadSceneAsyncCoroutine(scene.SceneName, onComplete));
        }

        IEnumerator LoadSceneAsyncCoroutine(string sceneName, System.Action onComplete) {
            yield return SceneManager.LoadSceneAsync(sceneName);
            onComplete();
        }
    }
}

