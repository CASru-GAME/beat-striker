
using Core.App.Types;

namespace Core.App.Presenters.Scene {

    public interface ISceneStateFactory {
        ISceneState CreateSceneState(AppScene scene, SceneStateContext context);
    }
}