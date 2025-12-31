using UnityEngine;

namespace Core.Battle {
    public class RoundStartState : IBattleState {
        private readonly IBattleModel model;

        public RoundStartState(IBattleModel model) {
            this.model = model;
        }

        public void Enter() {
            Debug.Log("Entering Round Start State");
            model.FireRoundStarted();
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
        }
    }
}
