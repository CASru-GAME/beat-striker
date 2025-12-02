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
        public class NotifyRoundFinishAnimationFinished { }
        public class NotifyOutroAnimationFinished { }

        public class NotifyPlayerDead {
            public readonly PlayerId playerId;
            public NotifyPlayerDead(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        public class OnBeat {
            public readonly PlayerId playerId;
            public readonly BeatResult result;
            public OnBeat(PlayerId playerId, BeatResult result) {
                this.playerId = playerId;
                this.result = result;
            }
        }

        public class OnBattleStarted {
            public readonly IBattlemodelGetter battlemodel;
            public OnBattleStarted(IBattlemodelGetter battlemodel) {
                this.battlemodel = battlemodel;
            }
        }

        public class OnBattleFinished {
            public readonly IBattlemodelGetter battlemodel;
            public OnBattleFinished(IBattlemodelGetter battlemodel) {
                this.battlemodel = battlemodel;
            }
        }

        public class OnRoundStarted {
            public readonly IBattlemodelGetter battlemodel;
            public OnRoundStarted(IBattlemodelGetter battlemodel) {
                this.battlemodel = battlemodel;
            }
        }

        public class OnOutroStarted {
            public readonly IBattlemodelGetter battlemodel;
            public OnOutroStarted(IBattlemodelGetter battlemodel) {
                this.battlemodel = battlemodel;
            }
        }

        public class RequireVictoryPose {
            public readonly PlayerId playerId;
            public RequireVictoryPose(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        public class OnResultStarted {
            public readonly IBattlemodelGetter battlemodel;
            public OnResultStarted(IBattlemodelGetter battlemodel) {
                this.battlemodel = battlemodel;
            }
        }

        public class RequestShowMenu { }

    }
}