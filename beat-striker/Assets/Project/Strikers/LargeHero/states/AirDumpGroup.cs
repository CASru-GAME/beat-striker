using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class AirDumpGroup : StrikerGroup {
        [SerializeField] float linearDamping;
        [SerializeField] AirDashState AirDashState;
        [SerializeField] StrikerNode attackNode;
        [SerializeField] StrikerNode chargeState;

        public override void OnEnter(IStrikerContext context) {
            Debug.Log("AirDumpGroup: OnEnter");
            context.Rigidbody.linearDamping = linearDamping;
        }

        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
            Debug.Log("AirDumpGroup: OnExit");
            context.Rigidbody.linearDamping = 0f;
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
            context.TryTransition(AirDashState);
        }

        // ガードコマンドが押された時に呼ばれる
        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        // 攻撃を受けた時に呼ばれる
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
        }

        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
