using Core.Utils;

namespace Core.Battle {
    public class RoundStartState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;

        public RoundStartState(IBattleStateMutator mutator, IBus bus) {
            this.mutator = mutator;
            this.bus = bus;
        }

        public void Enter() {
            bus.Subscribe<BattleMessages.NotifyRoundStartAnimationFinished>(OnRoundStartAnimationFinished);
        }

        public void Exit() {
            bus.Unsubscribe<BattleMessages.NotifyRoundStartAnimationFinished>(OnRoundStartAnimationFinished);
        }

        private void OnRoundStartAnimationFinished(BattleMessages.NotifyRoundStartAnimationFinished msg) {
            mutator.ChangeState(new RoundState(mutator, bus));
        }
    }
}
