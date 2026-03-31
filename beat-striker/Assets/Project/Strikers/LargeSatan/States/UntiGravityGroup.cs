using UnityEngine;
using Alice;

namespace Core.LargeSatan {

    public class UntilGravityGroup : StrikerGroup {
        bool previousUseGravity;

        // このグループに入った時に呼ばれる（前のステートがこのグループに所属していなかった場合）
        public override void OnEnter(IStrikerContext context) {
            this.previousUseGravity = context.Rigidbody.useGravity;
            context.Rigidbody.useGravity = false;
            context.Rigidbody.linearVelocity = Vector3.zero;
        }

        public override void OnUpdate(IStrikerStateContext context) {
        }

        // このグループから出る時に呼ばれる（次のステートがこのグループに所属していない場合）
        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.useGravity = this.previousUseGravity;
        }
    }
}
