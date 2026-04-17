using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class AirGroup : StrikerGroup {
        [SerializeField] float linearDamping;
        [SerializeField] AirDashState AirDashState;
        [SerializeField] StrikerNode attackNode;
        [SerializeField] StrikerNode chargeState;
        [SerializeField] StrikerNode locomotionNode;
        [SerializeField] StrikerNode stunState;
        [SerializeField] StrikerNode guardState;
        [SerializeField] StrikerNode SpecialState;

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

        // ダッシュコマンドが押された時に呼ばれる
        public override void OnDashRequested(IStrikerStateContext context) {
            context.TryTransition(AirDashState);
        }


        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            context.TryTransition(attackNode);
        }

        public override void OnChargeRequested(IStrikerStateContext context) {
            context.TryTransition(chargeState);
        }

        // ガードコマンドが押された時に呼ばれる
        public override void OnGuardRequested(IStrikerStateContext context) {
            context.TryTransition(guardState);
        }

        public override void OnSpecialRequested(IStrikerStateContext context) {
            context.TryTransition(SpecialState);
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
