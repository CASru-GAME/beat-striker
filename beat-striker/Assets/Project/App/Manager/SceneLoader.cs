using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alice {
    public enum AppScene {
        Title,
        CharacterSelect,
        StageSelect,
        Live,
        Street,
        ResultMenu,
        Boot,
    }

    public interface ISceneLoader {
        Task LoadAsync(AppScene scene);
    }

    public class SceneLoader : ISceneLoader {
        readonly IScreenRegistry appScreenRegistry;

        public SceneLoader(IScreenRegistry screenRegistry) {
            appScreenRegistry = screenRegistry;
        }

        public async Task LoadAsync(AppScene scene) {
            var sceneName = appScreenRegistry.GetByScene(scene).SceneName;
            if (string.IsNullOrWhiteSpace(sceneName)) {
                throw new InvalidOperationException($"SceneName is empty for AppScene '{scene}'.");
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName)) {
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' for AppScene '{scene}' is not available in the active Build Profile/shared scene list.");
            }

            var asyncOperation = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOperation == null) {
                throw new InvalidOperationException(
                    $"SceneManager.LoadSceneAsync returned null for scene '{sceneName}' (AppScene '{scene}').");
            }

            while (!asyncOperation.isDone) {
                await Task.Yield();
            }
        }
    }
}