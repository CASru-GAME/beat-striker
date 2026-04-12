using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {

    public class DashNode : StrikerNode {
        [SerializeField] StrikerNode jumpUpwardState;   
        [SerializeField] StrikerNode dashState;
        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            if (context.LocalInputDirection.y > 0) {
                context.TryTransition(jumpUpwardState);
            }
            if (context.LocalInputDirection.y == 0) {
                context.TryTransition(dashState);
            }
        }
        

    }
}
