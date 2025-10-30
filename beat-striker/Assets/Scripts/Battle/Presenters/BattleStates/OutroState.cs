using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class OutroState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;
        private readonly IRythmTrackModel rythmTrackModel;

        public OutroState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel, IRythmTrackModel rythmTrackModel) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.rythmTrackModel = rythmTrackModel;
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
