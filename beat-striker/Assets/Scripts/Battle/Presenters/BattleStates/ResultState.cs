using UnityEngine;

namespace Core.Battle {
    public class ResultState : IBattleState {
        private readonly IBattleModel model;

        public ResultState(IBattleModel model) {
            this.model = model;
        }

        public void Enter() {
            Debug.Log("Entering Result State");
            model.FireResultStarted();
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
        }
    }
}
