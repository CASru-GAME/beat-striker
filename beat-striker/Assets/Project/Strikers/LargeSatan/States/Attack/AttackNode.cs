using UnityEngine;
using Alice;

namespace Core.LargeSatan {

    public class AttackNode : StrikerNode {
        [SerializeField] StrikerState attackState, airDownState;
        [SerializeField] GroundChecker groundChecker;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if(!groundChecker.IsGrounded && context.InputDirection.y < -0.5f) {
                context.TryTransition(airDownState);
            }
            else {
                context.TryTransition(attackState);
            }
        }
        

    }
}
