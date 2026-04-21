using UnityEngine;
using Alice;

namespace Core.LargeSatan {

    public class AttackNode : StrikerNode {
        [SerializeField] StrikerState attackState, airDownState, airAttackState;
        [SerializeField] GroundChecker groundChecker;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if (!groundChecker.IsGrounded) {
                if (context.InputDirection.y < -0.5f) {
                    context.TryTransition(airDownState);
                }
                else {
                    context.TryTransition(airAttackState);
                }
            }
            else {
                context.TryTransition(attackState);
            }
        }
    }
}
