using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {

    public class DashNode : StrikerNode {
        [SerializeField] StrikerNode jumpUpwardNode;        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if (context.InputDirection.y > 0) {
                context.TryTransition(jumpUpwardNode);
            }
        }
        

    }
}
