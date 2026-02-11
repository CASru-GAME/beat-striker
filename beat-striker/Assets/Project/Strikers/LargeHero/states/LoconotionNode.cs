using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class LocomotionNode : StrikerNode {
        [SerializeField] StrikerNode idlestate;
        [SerializeField] StrikerNode walkstate;
        [SerializeField] StrikerNode walkbackwardstate;
        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if(context.InputDirection.x > 0) {
                context.TryTransition(walkstate);
            } else if(context.InputDirection.x < 0) {
                context.TryTransition(walkbackwardstate);
            } else {
                context.TryTransition(idlestate);
            }
        }
    }
}
