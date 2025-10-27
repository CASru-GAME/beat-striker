

using System.Linq;
using Core.App.Models;
using Core.App.Presenters.Scene.States;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;

namespace Core.App.Presenters.Scene {

    public class SceneStatePresenter : ISceneStateController, ISceneStateFactory {
        private ISceneState currentState;

        public SceneStatePresenter() {

        }

        public void ChangeState(ISceneState newState) {
            currentState?.Exit();
            currentState = newState;
            currentState.Enter();
        }

        public ISceneState CreateSceneState(AppScene scene, SceneStateContext context) {
            return scene switch {
                AppScene.Title => new TitleState(context),
                AppScene.StageSelect => new StageSelectState(context),
                AppScene.CharacterSelect => new CharacterSelectState(context),
                AppScene.Battle => new BattleState(context),
                _ => new TitleState(context),
            };
        }
    }
}