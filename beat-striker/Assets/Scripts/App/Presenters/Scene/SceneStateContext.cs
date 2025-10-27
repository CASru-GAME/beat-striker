using Core.App.Models;
using Core.Utils;

namespace Core.App.Presenters.Scene {

    public class SceneStateContext {
        public readonly ISceneView view;
        public readonly IBus bus;
        public readonly IBattleSettingModel setting;
        public ISceneStateController controller { get; set; }
        public ISceneStateFactory factory { get; set; }
    

        public SceneStateContext(
            ISceneView view,
            IBus bus,
            IBattleSettingModel setting,
            ISceneStateController controller,
            ISceneStateFactory factory
        
        ) {
            this.view = view;
            this.bus = bus;
            this.setting = setting;
            this.controller = controller;
            this.factory = factory;
        }

    }
}