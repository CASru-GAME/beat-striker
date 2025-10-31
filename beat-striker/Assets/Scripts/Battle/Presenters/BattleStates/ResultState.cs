using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class ResultState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;

        public ResultState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
        }

        public void Enter() {
            Debug.Log("Entering Result State");
            bus.Publish(new BattleMessages.OnResultStarted(battleModel));
            // Logic for entering the result state
        }

        public void OnUpdate(float deltaTime) {
            // Logic for updating the result state
        }

        public void Exit() {
            // Logic for exiting the result state
        }
    }
}
