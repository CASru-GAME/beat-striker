using Core.App.Types;
using UnityEngine;

namespace Core.Battle {
    public class RoundState : IBattleState {
        private readonly IBattleModel model;
        private readonly TrackId trackId;

        public RoundState(IBattleModel model, TrackId trackId) {
            this.model = model;
            this.trackId = trackId;
        }

        public void Enter() {
            Debug.Log("Entering Round State");
            // View should subscribe to BattleStarted to Play Track
            model.FireBattleStarted();
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            model.FireRoundFinished();
        }
    }
}
