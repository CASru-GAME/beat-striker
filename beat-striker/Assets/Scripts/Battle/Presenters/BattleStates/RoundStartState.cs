using Core.Utils;

namespace Core.Battle {
    public class RoundStartState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly BattleModel battleModel;
        private readonly StrikerModel strikerModel;
        private readonly RythmTrackModel rythmTrackModel;

        public RoundStartState(IBattleStateMutator mutator, IBus bus, BattleModel battleModel, StrikerModel strikerModel, RythmTrackModel rythmTrackModel) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.strikerModel = strikerModel;
            this.rythmTrackModel = rythmTrackModel;
        }

        public void Enter() {
            bus.Subscribe<BattleMessages.NotifyRoundStartAnimationFinished>(OnRoundStartAnimationFinished);
        }
        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            bus.Unsubscribe<BattleMessages.NotifyRoundStartAnimationFinished>(OnRoundStartAnimationFinished);
        }

        private void OnRoundStartAnimationFinished(BattleMessages.NotifyRoundStartAnimationFinished msg) {
            mutator.ChangeState(new RoundState(mutator, bus, battleModel, strikerModel, rythmTrackModel));
        }
    }
}
