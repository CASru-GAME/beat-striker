using UnityEngine;

namespace Core.Battle {
    public class RoundFinishState : IBattleState {
        private readonly IBattleModel model;

        public RoundFinishState(IBattleModel model) {
            this.model = model;
        }

        public void Enter() {
            Debug.Log("Entering Round Finish State");
            // Wait for animation (View hears LoserAdded?)
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
             ((BattleModel)model).ResetBattle();
             model.NextRound();
        }
    }
}
