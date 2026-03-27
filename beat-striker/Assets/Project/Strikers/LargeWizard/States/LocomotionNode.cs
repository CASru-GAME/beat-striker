using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {

    public class LocomotionNode : StrikerNode {
        [SerializeField] StrikerNode idleState;
        [SerializeField] StrikerNode walkState;
        [SerializeField] StrikerNode walkBackwardState;
        [SerializeField] GroundChecker groundChecker;
        [SerializeField] StrikerNode fallState;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if (!groundChecker.IsGrounded ) {
                context.TryTransition(fallState);
                return;
            }
            
            if (context.InputDirection.x > 0) {
                context.TryTransition(walkState);
            } else if (context.InputDirection.x < 0) {
                context.TryTransition(walkBackwardState);
            } else {
                context.TryTransition(idleState);
            }
        }
        

    }
}
