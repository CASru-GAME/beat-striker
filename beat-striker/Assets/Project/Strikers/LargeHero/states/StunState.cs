using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class StunState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        public override void OnEnter(IStrikerContext context) {
            Debug.Log("StunStateに遷移");
            context.PlayAnimation(animationClip, OnAnimationEnd);
        }
        void OnAnimationEnd(IStrikerStateContext context)
        {}
        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            context. TryTransition(nextNode);
        }

        public override void OnChargeRequested(IStrikerStateContext context) {
            context. TryTransition(nextNode);

        }

        // ダッシュコマンドが押された時に呼ばれる
        public override void OnDashRequested(IStrikerStateContext context) {
            context. TryTransition(nextNode);
        }

        // ガードコマンドが押された時に呼ばれる
        public override void OnGuardRequested(IStrikerStateContext context) {
            context. TryTransition(nextNode);

        }

        // 攻撃を受けた時に呼ばれる
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
            context.ApplyDamage(status.Damage);
        }

        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
