

using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Core.App.Presenters.Scene {

    public class SceneStateController : ISceneStateController {
        private ISceneState currentState;

        public void ChangeState(ISceneState newState) {
            currentState?.Exit();
            currentState = newState;
            currentState?.Enter();
        }
    }
}