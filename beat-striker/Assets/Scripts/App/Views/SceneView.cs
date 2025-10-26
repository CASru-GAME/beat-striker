
using System.Threading.Tasks;
using Core.App.Presenters.Scene;
using UnityEngine.SceneManagement;

namespace Core.App.Views.Scene {
    public class SceneView : ISceneView {

        public async Task LoadSceneAsync(string sceneName) {

            var operation = SceneManager.LoadSceneAsync(sceneName);
            while (!operation.isDone) {
                await Task.Yield();
            }
        }
    }
}