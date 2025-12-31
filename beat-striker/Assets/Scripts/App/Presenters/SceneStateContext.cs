using Core.App.Models;
using Core.Utils;

namespace Core.App.Presenters.Scene {

    public class SceneStateContext {
        public readonly ISceneView view;
        public readonly IBus bus;
        public readonly IBattleSettingModel setting;
        public readonly ICursorFactory cursorFactory;
        public readonly ICursorRegistry cursorRegistry;
        public readonly ISceneStateController controller;
        public readonly ISceneStateFactory factory;
        public readonly IPlayerRegistry playerRegistry;


        public SceneStateContext(
            ISceneView view,
            IBus bus,
            IBattleSettingModel setting,
            ISceneStateController controller,
            ISceneStateFactory factory,
            ICursorFactory cursorFactory,
            ICursorRegistry cursorRegistry,

            IPlayerRegistry playerRegistry
        ) {
            this.view = view;
            this.bus = bus;
            this.setting = setting;
            this.controller = controller;
            this.factory = factory;
            this.cursorFactory = cursorFactory;
            this.cursorRegistry = cursorRegistry;
            this.playerRegistry = playerRegistry;
        }

    }
}