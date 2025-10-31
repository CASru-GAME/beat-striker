namespace Core.App.Presenters.Scene {
    
    public interface ISceneStateController {
        void ChangeState(ISceneState newState);
    }
}