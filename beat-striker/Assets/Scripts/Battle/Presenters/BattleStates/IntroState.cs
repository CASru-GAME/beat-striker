using Core.Utils;

namespace Core.Battle {
    public class IntroState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;

        public IntroState(IBattleStateMutator mutator, IBus bus) {
            this.mutator = mutator;
            this.bus = bus;
        }

        public void Enter() {
            bus.Subscribe<BattleMessages.NotifyIntroAnimationFinished>(OnIntroAnimationFinished);
        }

        public void Exit() {
            bus.Unsubscribe<BattleMessages.NotifyIntroAnimationFinished>(OnIntroAnimationFinished);
        }

        private void OnIntroAnimationFinished(BattleMessages.NotifyIntroAnimationFinished msg) {
            mutator.ChangeState(new RoundStartState(mutator, bus));
        }
    }
}
