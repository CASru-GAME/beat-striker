using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Alice {
    /// <summary>
    /// 列挙子の整数値はインスペクタ等に保存されるため、Menu 追加以前の値 (0〜6) を変えないこと。
    /// </summary>
    public enum AppScene {
        Title = 0,
        CharacterSelect = 1,
        StageSelect = 2,
        Live = 3,
        Street = 4,
        ResultMenu = 5,
        Boot = 6,
        Menu = 7,
    }

    public interface ISceneLoader {
        Task LoadAsync(AppScene scene);
    }

    public class SceneLoader : ISceneLoader {
        readonly IScreenRegistry appScreenRegistry;
        readonly ILoadingOverlayService loadingOverlayService;

        [Inject]
        public SceneLoader(IScreenRegistry screenRegistry, ILoadingOverlayService loadingOverlayService) {
            appScreenRegistry = screenRegistry;
            this.loadingOverlayService = loadingOverlayService;
        }

        public async Task LoadAsync(AppScene scene) {
            using var scope = loadingOverlayService.Begin();
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