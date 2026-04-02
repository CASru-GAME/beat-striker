using UnityEngine;
using Alice;

namespace Core.LargeSatan {
    
    public class TurnableGroup : StrikerGroup {
        [SerializeField] StrikerNode turnState;

        // このグループに入った時に呼ばれる（前のステートがこのグループに所属していなかった場合）
        public override void OnEnter(IStrikerContext context) {
        }

        // このグループから出る時に呼ばれる（次のステートがこのグループに所属していない場合）
        public override void OnExit(IStrikerContext context) {
        }

        public override void OnEnemyBehind(IStrikerStateContext context) {
            context.TryTransition(turnState, true);
        }

    }
}
