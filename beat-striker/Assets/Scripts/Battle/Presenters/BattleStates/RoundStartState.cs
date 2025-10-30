using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class RoundStartState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;
        private readonly IRythmTrackModel rythmTrackModel;

        public RoundStartState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel, IRythmTrackModel rythmTrackModel) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.rythmTrackModel = rythmTrackModel;
        }

        public void Enter() {
            Debug.Log("Entering Round Start State");
            bus.Subscribe<BattleMessages.NotifyRoundStartAnimationFinished>(OnRoundStartAnimationFinished);
            bus.Publish(new BattleMessages.OnRoundStarted(battleModel.GetCurrentRound()));
        }
        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            bus.Unsubscribe<BattleMessages.NotifyRoundStartAnimationFinished>(OnRoundStartAnimationFinished);
        }

        private void OnRoundStartAnimationFinished(BattleMessages.NotifyRoundStartAnimationFinished msg) {
            mutator.ChangeState(new RoundState(mutator, bus, battleModel, rythmTrackModel));
            
        }
    }
}
