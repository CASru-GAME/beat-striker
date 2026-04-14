using UnityEngine;
using Alice;

namespace Core.LargeHero {
    
    public class TurnableGroup : StrikerGroup {
        [SerializeField] StrikerNode turnState;

        public override void OnEnter(IStrikerContext context) {
        }

        public override void OnExit(IStrikerContext context) {
        }

        public override void OnEnemyBehind(IStrikerStateContext context) {
            context.TryTransition(turnState, true);
        }

    }
}
