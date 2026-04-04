
using Core.App.Types;

namespace Core.App.Presenters.Scene {

    public interface ISceneStateFactory {
        ISceneState CreateSceneState(FAFA scene, SceneStateContext context);
    }
}