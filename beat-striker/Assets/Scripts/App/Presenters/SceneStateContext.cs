using Core.App.Interfaces;
using Core.App.Models;
using Core.Utils;

namespace Core.App.Presenters.Scene {

    public class SceneStateContext {
        public readonly ISceneView view;
        public readonly IAppModel events; // Keeping name 'events' to minimize churn
        public readonly IBattleSettingModel setting;
        public readonly ICursorFactory cursorFactory;
        public readonly ICursorRegistry cursorRegistry;
        public readonly ISceneStateController controller;
        public readonly ISceneStateFactory factory;
        public readonly IPlayerRegistry playerRegistry;


        public SceneStateContext(
            ISceneView view,
            IAppModel events,
            IBattleSettingModel setting,
            ISceneStateController controller,
            ISceneStateFactory factory,
            ICursorFactory cursorFactory,
            ICursorRegistry cursorRegistry,

            IPlayerRegistry playerRegistry
        ) {
            this.view = view;
            this.events = events;
            this.setting = setting;
            this.controller = controller;
            this.factory = factory;
            this.cursorFactory = cursorFactory;
            this.cursorRegistry = cursorRegistry;
            this.playerRegistry = playerRegistry;
        }

    }
}