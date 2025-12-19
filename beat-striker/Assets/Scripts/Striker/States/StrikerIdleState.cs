using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    [AddComponentMenu("Striker/States/Idle State")]
    public class StrikerIdleState : StrikerState {
        [SerializeField] private AnimationClip animationClip;

        public override void Enter() {
            if (animationClip != null) hub.PlayAnimation(animationClip);
        }

        public override void OnUpdate() {
            // Idle logic
        }
    }
}
