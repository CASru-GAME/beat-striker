using UnityEngine;
using Alice;

namespace Core.LargeSatan {

    public class LocomotionNode : StrikerNode {
        [SerializeField] StrikerNode idleState;
        [SerializeField] StrikerNode walkState;
        [SerializeField] StrikerNode walkBackwardState;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if(context.LocalInputDirection.x > 0) {
                context.TryTransition(walkState);
            }
            else if(context.LocalInputDirection.x < 0) {
                context.TryTransition(walkBackwardState);
            }
            else {
                context.TryTransition(idleState);
            }
        }
        

    }
}
