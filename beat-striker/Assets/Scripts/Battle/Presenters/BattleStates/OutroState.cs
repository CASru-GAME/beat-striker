using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class OutroState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;
        private readonly IRythmTrackModel rythmTrackModel;
        private readonly IBattleResetter resetter;

        public OutroState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel, IRythmTrackModel rythmTrackModel, IBattleResetter resetter) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.rythmTrackModel = rythmTrackModel;
            this.resetter = resetter;
        }

        public void Enter() {
            Debug.Log("Entering Outro State");
            bus.Publish(new BattleMessages.OnOutroStarted(battleModel.GetWinner(battleModel.GetCurrentRound())));
            // Logic for entering the outro state
        }
        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            // Logic for exiting the outro state
        }
    }
}
