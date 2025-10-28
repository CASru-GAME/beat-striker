using Core.Utils;

namespace Core.Battle {
    public class OutroState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;

        public OutroState(IBattleStateMutator mutator, IBus bus) {
            this.mutator = mutator;
            this.bus = bus;
        }

        public void Enter() {
            // Logic for entering the outro state
        }

        public void Exit() {
            // Logic for exiting the outro state
        }
    }
}
