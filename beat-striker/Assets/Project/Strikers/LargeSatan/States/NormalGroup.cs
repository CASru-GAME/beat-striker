using UnityEngine;
using Alice;

namespace Core.LargeSatan {
    
    public class NormalGroup : StrikerGroup {
        [SerializeField] StrikerNode dashNode;
        [SerializeField] StrikerNode attackNode;
        [SerializeField] StrikerNode stunState;
        [SerializeField] StrikerNode guardState;
        [SerializeField] StrikerNode chargeState;
        [SerializeField] StrikerNode specialState;

        // このグループに入った時に呼ばれる（前のステートがこのグループに所属していなかった場合）
        public override void OnEnter(IStrikerContext context) {
        }

        // このグループから出る時に呼ばれる（次のステートがこのグループに所属していない場合）
        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            context.TryTransition(attackNode, true);
        }

        // チャージコマンドが押された時に呼ばれる
        public override void OnChargeRequested(IStrikerStateContext context) {
            context.TryTransition(chargeState, true);
        }

        // ダッシュコマンドが押された時に呼ばれる
        public override void OnDashRequested(IStrikerStateContext context) {
            context.TryTransition(dashNode, true);
        }

        // ガードコマンドが押された時に呼ばれる
        public override void OnGuardRequested(IStrikerStateContext context) {
            context.TryTransition(guardState, true);
        }

        // 攻撃を受けた時に呼ばれる
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
            context.Rigidbody.linearVelocity = status.KnockbackVelocity;
            context.ApplyDamage(status.Damage);
            context.TryTransition(stunState, true);
        }

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

        public override void OnSpecialRequested(IStrikerStateContext context) {
            context.TryTransition(specialState, true);
        }
    }
}
