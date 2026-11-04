using UnityEngine;
using Alice;

namespace Core.DarkSatan {

    public class AttackNode : StrikerNode {
        [SerializeField] StrikerState attackState, airDownState, airAttackState;
        [SerializeField] GroundChecker groundChecker;
        [SerializeField] StrikerState upAttackState, downAttackState;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if (context.InputDirection.y > 0.5f) {
                context.TryTransition(upAttackState);
                return;
            }
            if (context.InputDirection.y < -0.5f) {
                context.TryTransition(downAttackState);
                return;
            }
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
