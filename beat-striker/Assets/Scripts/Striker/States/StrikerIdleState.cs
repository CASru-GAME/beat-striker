using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    [AddComponentMenu(" StrikerStates/Idle State")]
    public class StrikerIdleState : StrikerState {
        [SerializeField] private AnimationClip animationClip;

        public override void Enter(IStrikerHub hub) {
            if (animationClip != null) hub.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerHub hub) {
            // Idle logic
        }
    }
}
