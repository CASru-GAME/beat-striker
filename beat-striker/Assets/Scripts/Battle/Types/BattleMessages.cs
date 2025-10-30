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

        public class NotifyPlayerDead {
            public readonly PlayerId playerId;
            public NotifyPlayerDead(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        public class OnBattleStarted {
            public readonly int round;
            public OnBattleStarted(int round) {
                this.round = round;
            }
        }

        public class OnBattleFinished {
            public readonly int round;
            public OnBattleFinished(int round) {
                this.round = round;
            }
        }

        public class OnRoundStarted {
            public readonly int round;
            public OnRoundStarted(int round) {
                this.round = round;
            }
        }

        public class OnOutroStarted {
            public readonly PlayerId winner;
            public OnOutroStarted(PlayerId winner) {
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