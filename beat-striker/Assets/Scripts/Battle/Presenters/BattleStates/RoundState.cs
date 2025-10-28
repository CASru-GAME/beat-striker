using Core.Utils;

namespace Core.Battle {
    public class RoundState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly BattleModel battleModel;
        private readonly StrikerModel strikerModel;
        private readonly RythmTrackModel rythmTrackModel;

        public RoundState(IBattleStateMutator mutator, IBus bus, BattleModel battleModel, StrikerModel strikerModel, RythmTrackModel rythmTrackModel) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.strikerModel = strikerModel;
            this.rythmTrackModel = rythmTrackModel;
        }

        public void Enter() {
            bus.Publish(new BattleMessages.OnRoundStart(battleModel.GetCurrentRound()));
            bus.Subscribe<BattleMessages.NotifyPlayerDead>(ProcessPlayerDeathNotification);
        }

        public void OnUpdate(float deltaTime) {
            rythmTrackModel.AddTime(deltaTime);
        }

        public void Exit() {
            bus.Publish(new BattleMessages.OnRoundFinished(battleModel.GetWinner(battleModel.GetCurrentRound())));
            bus.Unsubscribe<BattleMessages.NotifyPlayerDead>(ProcessPlayerDeathNotification);
        }

        private void ProcessPlayerDeathNotification(BattleMessages.NotifyPlayerDead message) {
            battleModel.AddLoser(message.playerId);
            if (!battleModel.IsFinished()) {
                mutator.ChangeState(new RoundStartState(mutator, bus, battleModel, strikerModel, rythmTrackModel));
                battleModel.NextRound();
            }
            else {
                mutator.ChangeState(new OutroState(mutator, bus, battleModel, strikerModel, rythmTrackModel));
            }
        }
    }
}
