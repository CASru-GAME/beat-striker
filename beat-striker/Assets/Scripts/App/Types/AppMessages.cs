
using Core.App.Types;

namespace Core.App.Presenters.Scene.Types {

    public class RequireTransitionMessage {
        public readonly TransitionRequire command;
        public readonly AppScene scene;

        public RequireTransitionMessage(TransitionRequire command) {
            this.command = command;
            this.scene = AppScene.None;
        }

        public RequireTransitionMessage(AppScene scene) {
            this.command = TransitionRequire.Next;
            this.scene = scene;
        }
    }


    public class TransitionStartedMessage {
        public readonly AppScene scene;

        public TransitionStartedMessage(AppScene scene) {
            this.scene = scene;
        }
    }

    public class SelectStageMessage {
        public readonly StageId stage;

        public SelectStageMessage(StageId stage) {
            this.stage = stage;
        }
    }

    public class SelectStrikerMessage {
        public readonly StrikerId striker;
        public readonly PlayerId playerId;

        public SelectStrikerMessage(PlayerId playerId, StrikerId striker) {
            this.playerId = playerId;
            this.striker = striker;
        }
    }

    public class SelectTrackMessage {
        public readonly TrackId track;

        public SelectTrackMessage(TrackId track) {
            this.track = track;
        }
    }
}