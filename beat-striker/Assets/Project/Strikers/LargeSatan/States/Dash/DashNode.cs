using UnityEngine;
using Alice;

namespace Core.LargeSatan {

    public class DashNode : StrikerNode {
        [SerializeField] StrikerNode jumpNode, dashNode;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if(context.InputDirection.y > 0.5f) {
                context.TryTransition(jumpNode);
            } else {
                context.TryTransition(dashNode);
            }
        }
    }
}
