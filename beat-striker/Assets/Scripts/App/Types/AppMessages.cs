
using Core.App.Types;

namespace Core.App.Presenters.Scene.Types {

    public class TransitionMessage {
        public readonly TransitionCommand command;
        public readonly AppScene scene;

        public TransitionMessage(TransitionCommand command) {
            this.command = command;
            this.scene = AppScene.None;
        }

        public TransitionMessage(AppScene scene) {
            this.command = TransitionCommand.Next;
            this.scene = scene;
        }


    }
}