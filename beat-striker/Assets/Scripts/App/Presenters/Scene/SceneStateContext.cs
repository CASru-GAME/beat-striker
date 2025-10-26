using Core.App.Models;
using Core.Utils;

namespace Core.App.Presenters.Scene {

    public class SceneStateContext {
        public readonly ISceneStateController controller;
        public readonly ISceneStateFactory factory;
        public readonly ISceneView view;
        public readonly Bus bus;

        public SceneStateContext(
            ISceneStateController controller,
            ISceneStateFactory factory,
            ISceneView view,
            Bus bus
        ) {
            this.controller = controller;
            this.factory = factory;
            this.view = view;
            this.bus = bus;
        }

    }
}