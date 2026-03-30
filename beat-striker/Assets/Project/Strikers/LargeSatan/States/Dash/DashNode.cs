using UnityEngine;
using Alice;

namespace Core.LargeSatan {

    public class DashNode : StrikerNode {
        [SerializeField] StrikerNode jumpNode, dashNode, airJumpNode;
        [SerializeField] float jumpThreshold = 0.5f;
        [SerializeField] GroundChecker groundChecker;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if (groundChecker.IsGrounded) {
                if (context.InputDirection.y > jumpThreshold) {
                    context.TryTransition(jumpNode);
                }
                else {
                    context.TryTransition(dashNode);
                }
            }
            else {
                context.TryTransition(airJumpNode);
            }
        }
    }
}
