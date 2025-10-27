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


        public SceneStateContext(
            ISceneView view,
            IBus bus,
            IBattleSettingModel setting,
            ISceneStateController controller,
            ISceneStateFactory factory,
            ICursorFactory cursorFactory,
            ICursorRegistry cursorRegistry
        ) {
            this.view = view;
            this.bus = bus;
            this.setting = setting;
            this.controller = controller;
            this.factory = factory;
            this.cursorFactory = cursorFactory;
            this.cursorRegistry = cursorRegistry;
        }

    }
}