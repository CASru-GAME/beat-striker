using UnityEngine;

namespace Core.LargeHero {
    public class SpecialEntryNode : StrikerNode {
        [SerializeField] SpecialApproachState approachState;
        [SerializeField] StrikerNode launchState;

        public override void OnTryTransition(IStrikerNodeContext context) {
            context.TryTransition(approachState.IsOpponentInRange(context) ? launchState : approachState);
        }
    }
}
