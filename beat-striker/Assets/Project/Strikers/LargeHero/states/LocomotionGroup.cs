using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class LocomotionGroup : StrikerGroup {
        [SerializeField] StrikerNode locomotionNode;
        [SerializeField] StrikerNode attackNode;
        [SerializeField] StrikerNode dashNode;
        [SerializeField] StrikerNode stunState;
        [SerializeField] StrikerNode guardState;
        [SerializeField] StrikerNode chargeState;

        public override void OnEnter(IStrikerContext context) {
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.TryTransition(locomotionNode);
        }

        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            context.TryTransition(attackNode);
        }

        public override void OnChargeRequested(IStrikerStateContext context) {
            context.TryTransition(chargeState);
        }

        // ダッシュコマンドが押された時に呼ばれる
        public override void OnDashRequested(IStrikerStateContext context) {
            context.TryTransition(dashNode);
        }

        // ガードコマンドが押された時に呼ばれる
        public override void OnGuardRequested(IStrikerStateContext context) {
            context.TryTransition(guardState);
        }

        // 攻撃を受けた時に呼ばれる
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
            context.Rigidbody.linearVelocity = status.KnockbackVelocity;
            context.ApplyDamage(status.Damage);
            if (context.GetSelf().HitPoint.CurrentValue <= 0f) {
                return;
            }
            context.TryTransition(stunState);
        }

        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
