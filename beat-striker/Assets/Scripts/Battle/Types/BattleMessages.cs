using Core.App.Types;

namespace Core.Battle {
    public static class BattleMessages {

        public class RequireIntroPose {
            public readonly PlayerId playerId;
            public RequireIntroPose(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        public class NotifyIntroAnimationFinished { }

        public class NotifyRoundStartAnimationFinished { }

        public class OnRoundStart {
            public readonly int round;
            public OnRoundStart(int round) {
                this.round = round;
            }
        }

        public class NotifyPlayerDead {
            public readonly PlayerId playerId;
            public NotifyPlayerDead(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        public class OnRoundFinished {
            public readonly PlayerId winner;
            public OnRoundFinished(PlayerId winner) {
                this.winner = winner;
            }
        }

        public class RequireVictoryPose {
            public readonly PlayerId playerId;
            public RequireVictoryPose(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        

        

        
        

        
        

    }
}