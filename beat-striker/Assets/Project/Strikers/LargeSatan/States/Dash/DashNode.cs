using UnityEngine;
using Alice;

namespace Core.LargeSatan {

    public class DashNode : StrikerNode {
        [SerializeField] StrikerNode jumpUpwardNode, jumpBackwardNode;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if(context.InputDirection.y > 0) {
                if(context.InputDirection.x > 0) {
                    context.TryTransition(jumpUpwardNode);
                } else {
                    context.TryTransition(jumpBackwardNode);
                }
            }
        }
        

    }
}
