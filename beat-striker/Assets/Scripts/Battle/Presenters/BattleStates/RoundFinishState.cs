using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class RoundFinishState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;
        private readonly IRythmTrackModel rythmTrackModel;
        private readonly IBattleResetter resetter;

        public RoundFinishState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel, IRythmTrackModel rythmTrackModel, IBattleResetter resetter) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.rythmTrackModel = rythmTrackModel;
            this.resetter = resetter;
        }

        public void Enter() {
            Debug.Log("Entering Round Finish State");
            bus.Subscribe<BattleMessages.NotifyRoundFinishAnimationFinished>(OnRoundFinishAnimationFinished);
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            resetter.ResetBattle();
            battleModel.NextRound();
            bus.Unsubscribe<BattleMessages.NotifyRoundFinishAnimationFinished>(OnRoundFinishAnimationFinished);
        }

        private void OnRoundFinishAnimationFinished(BattleMessages.NotifyRoundFinishAnimationFinished msg) {
            Debug.Log("Round Finish Animation Finished");
            mutator.ChangeState(new RoundStartState(mutator, bus, battleModel, rythmTrackModel, resetter));
        }
    }
}
