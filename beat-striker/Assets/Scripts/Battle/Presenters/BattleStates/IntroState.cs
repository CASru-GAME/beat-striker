using Core.Utils;

namespace Core.Battle {
    public class IntroState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly BattleModel battleModel;
        private readonly StrikerModel strikerModel;
        private readonly RythmTrackModel rythmTrackModel;

        public IntroState(IBattleStateMutator mutator, IBus bus, BattleModel battleModel, StrikerModel strikerModel, RythmTrackModel rythmTrackModel) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.strikerModel = strikerModel;
            this.rythmTrackModel = rythmTrackModel;
        }

        public void Enter() {
            bus.Subscribe<BattleMessages.NotifyIntroAnimationFinished>(OnIntroAnimationFinished);
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            bus.Unsubscribe<BattleMessages.NotifyIntroAnimationFinished>(OnIntroAnimationFinished);
        }

        private void OnIntroAnimationFinished(BattleMessages.NotifyIntroAnimationFinished msg) {
            mutator.ChangeState(new RoundStartState(mutator, bus, battleModel, strikerModel, rythmTrackModel));
        }
    }
}
