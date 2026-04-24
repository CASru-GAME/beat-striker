using UnityEngine;
using Alice;
using System;

namespace Core.LargeWizard {

    public class StunGroup : StrikerGroup {
        bool isCancelled = false;

        public bool IsCancelled => isCancelled;

        public void CancelStun() {
            isCancelled = true;
        }

        // このグループに入った時に呼ばれる（前のステートがこのグループに所属していなかった場合）
        public override void OnEnter(IStrikerContext context) {
            isCancelled = false;
        }

        // このグループから出る時に呼ばれる（次のステートがこのグループに所属していない場合）
        public override void OnExit(IStrikerContext context) {
            isCancelled = false;
        }

    }
}
