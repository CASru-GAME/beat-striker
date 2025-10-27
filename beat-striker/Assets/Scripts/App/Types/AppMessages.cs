
using Core.App.Types;

namespace Core.App.Presenters.Scene.Types {

    public static class AppMessages {
        public class RequireTransition {
            public readonly TransitionRequire command;
            public readonly AppScene scene;

            public RequireTransition(TransitionRequire command) {
                this.command = command;
                this.scene = AppScene.None;
            }

            public RequireTransition(AppScene scene) {
                this.command = TransitionRequire.Next;
                this.scene = scene;
            }
        }

        public class RequireCursorDestroyed {
            private readonly bool isAll;
            private readonly PlayerId playerId;

            public RequireCursorDestroyed(PlayerId playerId) {
                this.playerId = playerId;
                isAll = false;
            }

            public RequireCursorDestroyed() {
                isAll = true;
            }

            public bool IsTarget(PlayerId playerId) {
                if (isAll) return true;
                return this.playerId.Equals(playerId);
            }
        }

        public class PlayerJoined {
            public readonly PlayerId playerId;

            public PlayerJoined(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        public class PlayerLeft {
            public readonly PlayerId playerId;

            public PlayerLeft(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        public class TransitionStartedMessage {
            public readonly AppScene scene;

            public TransitionStartedMessage(AppScene scene) {
                this.scene = scene;
            }
        }

        public class SelectStage {
            public readonly StageId stage;

            public SelectStage(StageId stage) {
                this.stage = stage;
            }
        }

        public class SelectStriker {
            public readonly StrikerId striker;
            public readonly PlayerId playerId;

            public SelectStriker(PlayerId playerId, StrikerId striker) {
                this.playerId = playerId;
                this.striker = striker;
            }
        }

        public class SelectTrack {
            public readonly TrackId track;

            public SelectTrack(TrackId track) {
                this.track = track;
            }
        }

    }




}