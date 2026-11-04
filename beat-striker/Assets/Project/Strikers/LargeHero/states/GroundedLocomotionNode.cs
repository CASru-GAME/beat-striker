using UnityEngine;

namespace Core.LargeHero {
    public sealed class GroundedLocomotionNode : StrikerNode {
        [SerializeField] GroundChecker groundChecker;
        [SerializeField] StrikerNode idleState;
        [SerializeField] StrikerNode walkState;
        [SerializeField] StrikerNode walkBackwardState;
        [SerializeField] StrikerNode fallState;

        public override void OnTryTransition(IStrikerNodeContext context) {
            if (!groundChecker.IsGrounded) {
                context.TryTransition(fallState);
                return;
            }
            context.TryTransition(context.LocalInputDirection.x > 0f ? walkState
                : context.LocalInputDirection.x < 0f ? walkBackwardState : idleState);
        }
    }
}
