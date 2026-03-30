using UnityEngine;
using Alice;
using R3;

namespace Core.LargeSatan {

    public class DashNode : StrikerNode {
        [SerializeField] StrikerNode jumpNode, dashNode, airJumpNode, airSuperJumpNode;
        [SerializeField] float jumpThreshold = 0.5f;
        [SerializeField] GroundChecker groundChecker;
        int airJumpCounter = 0;

        public void Awake() {
            groundChecker.IsGroundedProperty.Subscribe(isGrounded => {
                if (isGrounded) {
                    airJumpCounter = 0;
                }
            }).AddTo(this);
        }

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if (groundChecker.IsGrounded) {
                if (context.InputDirection == Vector2.zero || context.InputDirection.y > jumpThreshold) {
                    context.TryTransition(jumpNode);
                }
                else {
                    context.TryTransition(dashNode);
                }
            }
            else {
                airJumpCounter++;
                if (airJumpCounter % 2 == 1) {
                    context.TryTransition(airJumpNode);
                }
                else {
                    context.TryTransition(airSuperJumpNode);
                }
            }
        }
    }
}
