using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeSatan {
    
    public abstract class LocomotionGroupState : StrikerState {

        [SerializeField] StrikerNode locomotionNode;
        [SerializeField] StrikerNode dashNode;

        // このステートにいる間、毎フレーム呼ばれる
        public sealed override void OnUpdate(IStrikerStateContext context) {
            context.TryTransition(locomotionNode);
            OnLocomotionUpdate(context);
        }

        public abstract void OnLocomotionUpdate(IStrikerStateContext context);


        // ダッシュコマンドが押された時に呼ばれる
        public sealed override void OnDashRequested(IStrikerStateContext context) {
            context.TryTransition(dashNode);
            
        }

    }
}
