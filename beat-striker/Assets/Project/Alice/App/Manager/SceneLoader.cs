using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public enum AppScene {
        Title,
        CharacterSelect,
        StageSelect,
        Live,
        Street,
        ResultMenu,
    }

    public interface ISceneLoader {
        Task LoadAsync(AppScene scene);
    }

    public class SceneLoader : MonoBehaviour, ISceneLoader {
        IScreenRegistry appScreenRegistry;

        [Inject]
        public void Construct(IScreenRegistry screenRegistry) {
            appScreenRegistry = screenRegistry;
        }

        public async Task LoadAsync(AppScene scene) {
            try {
                var sceneName = appScreenRegistry.GetByScene(scene).SceneName;

                var asyncOperation = SceneManager.LoadSceneAsync(sceneName);
                while (!asyncOperation.isDone) {
                    await Task.Yield();
                }
            }
            catch (Exception e) {
                Debug.LogError($"Failed to load scene {scene}: {e}");
            }
        }
    }
}