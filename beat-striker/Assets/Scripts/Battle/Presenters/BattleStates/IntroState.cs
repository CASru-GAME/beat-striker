using Core.Utils;

namespace Core.Battle {
    public class IntroState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;
        private readonly IRythmTrackModel rythmTrackModel;

        public IntroState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel, IRythmTrackModel rythmTrackModel) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
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
            mutator.ChangeState(new RoundStartState(mutator, bus, battleModel, rythmTrackModel));
        }
    }
}
