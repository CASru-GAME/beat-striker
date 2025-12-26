using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    public abstract class StrikerState : StrikerNode, IStrikerState {

        public sealed override void OnTryTransition(IStrikerNodeContext context) {
            context.ChangeState(this);
        }

        public abstract void OnEnter(IStrikerContext hub);

        public abstract void OnUpdate(IStrikerStateContext hub);

        public abstract void OnExit(IStrikerContext hub);
        
        public abstract void OnAttackRequested(IStrikerStateContext hub);

        public abstract void OnChargeRequested(IStrikerStateContext hub);

        public abstract void OnDashRequested(IStrikerStateContext hub);

        public abstract void OnGuardRequested(IStrikerStateContext hub);

        public abstract void OnHit(IStrikerStateContext hub, HitStatus status);

        public abstract void OnMiss(IStrikerStateContext hub);
    }
}
