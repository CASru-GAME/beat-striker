using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeSatan {

    public class AttackNode : StrikerNode {
        [SerializeField] StrikerState attackState;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            context.TryTransition(attackState);
        }
        

    }
}
