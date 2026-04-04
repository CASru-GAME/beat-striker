

using System.Linq;
using Core.App.Models;
using Core.App.Presenters.Scene.States;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;

namespace Core.App.Presenters.Scene {

    public class SceneStatePresenter : ISceneStateController, ISceneStateFactory {
        private ISceneState currentState;

        public SceneStatePresenter(
            FAFA firstScene,
            ISceneView view,
            IBus bus,
            IBattleSettingModel setting,
            ICursorFactory cursorFactory,
            ICursorRegistry cursorRegistry,
            ILife life,
            IPlayerRegistry playerRegistry) {
            var context = new SceneStateContext(view, bus, setting, this, this, cursorFactory, cursorRegistry, playerRegistry);
            currentState = CreateSceneState(firstScene, context);
            currentState.Enter();
            life.Link(OnEnable, OnDisable);
        }

        private void OnEnable() {
        }
        private void OnDisable() {
            currentState.Exit();
        }

        public void ChangeState(ISceneState newState) {
            currentState.Exit();
            currentState = newState;
            currentState.Enter();
        }

        public ISceneState CreateSceneState(FAFA scene, SceneStateContext context) {
            return scene switch {
                FAFA.Title => new TitleState(context),
                FAFA.Menu => new MenuState(context),
                FAFA.StageSelect => new StageSelectState(context),
                FAFA.CharacterSelect => new CharacterSelectState(context),
                FAFA.Battle => new BattleState(context),
                FAFA.Battle_Stage => new BattleState(context),
                FAFA.Battle_Street => new BattleState(context),
                _ => new TitleState(context),
            };
        }
    }
}