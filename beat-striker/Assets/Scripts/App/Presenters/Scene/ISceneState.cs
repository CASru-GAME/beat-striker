using System.Threading.Tasks;
using Core.App.Presenters.Scene.Types;
using Core.Utils;
using UnityEngine.SceneManagement;

namespace Core.App.Presenters.Scene {

    public interface ISceneState {
        void Enter();
        void Exit();
    }
}