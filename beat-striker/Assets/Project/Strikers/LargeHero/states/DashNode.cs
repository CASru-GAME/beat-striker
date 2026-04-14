using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class DashNode : StrikerNode {
       [SerializeField] StrikerNode jumpUpwardNode;
        public override void OnTryTransition(IStrikerNodeContext context) {
            if (context.LocalInputDirection.y >= 0) {
                context.TryTransition(jumpUpwardNode);
            }
        }
        

    }
}
