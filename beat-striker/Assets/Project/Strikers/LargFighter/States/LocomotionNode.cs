using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargFighter {

    public class LocomotionNode : StrikerNode {
        [SerializeField] StrikerNode idleState;
        [SerializeField] StrikerNode walkState;
        [SerializeField] StrikerNode walkBackwardState;
        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if (context.InputDirection.x > 0) {
                context.TryTransition(walkState);
            }
            else if(context.InputDirection.x < 0) {
                context.TryTransition
            }
            else {
                context.TryTransition(idleState);
            }
            
        }
        

    }
}
