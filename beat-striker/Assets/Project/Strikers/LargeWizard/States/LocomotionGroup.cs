using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {
    
    public class LocomotionGroup : StrikerGroup {
        [SerializeField] StrikerNode locomotionNode;
        [SerializeField] StrikerNode dashNode;
        [SerializeField] StrikerNode attackNode;
        [SerializeField] StrikerNode stunState;
        [SerializeField] StrikerNode guardState;
        [SerializeField] StrikerNode chargeState;

        // このグループに入った時に呼ばれる（前のステートがこのグループに所属していなかった場合）
        public override void OnEnter(IStrikerContext context) {
        }

        // このグループに所属するステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            context.TryTransition(locomotionNode);
        }

        // このグループから出る時に呼ばれる（次のステートがこのグループに所属していない場合）
        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            context.TryTransition(attackNode);
        }

        // チャージコマンドが押された時に呼ばれる
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
            context.TryTransition(stunState);
            context.ApplyDamage(status.Damage);
        }

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
