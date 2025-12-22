using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    [AddComponentMenu(" StrikerStates/Idle State")]
    public class StrikerIdleState : StrikerState {
        [SerializeField] private AnimationClip animationClip;

        public override void Enter(StrikerStateContext context) {
            if (animationClip != null) context.Hub.PlayAnimation(animationClip);
        }

        public override void OnUpdate(StrikerStateContext context) {
            // Idle logic
        }
    }
}
