using UnityEngine;
using Core.App.Types;

namespace Core.Battle {
    public class OutroState : IBattleState {
        private readonly IBattleModel model;

        public OutroState(IBattleModel model) {
            this.model = model;
        }

        public void Enter() {
            Debug.Log("Entering Outro State");
            model.FireOutroStarted();
            
            var winner = model.GetFinalWinner();
            model.FireRequireVictoryPose(winner);
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
        }
    }
}
