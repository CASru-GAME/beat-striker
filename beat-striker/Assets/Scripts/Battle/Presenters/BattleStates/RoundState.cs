using Core.Utils;

namespace Core.Battle {
    public class RoundState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;

        public RoundState(IBattleStateMutator mutator, IBus bus) {
            this.mutator = mutator;
            this.bus = bus;
        }

        public void Enter() {
            // Logic for entering the round state
        }

        public void Exit() {
            // Logic for exiting the round state
        }
    }
}
