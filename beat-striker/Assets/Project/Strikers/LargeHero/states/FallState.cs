using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class FallState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] GroundChecker groundChecker;
        [SerializeField] StrikerNode landNode;
        [SerializeField] StrikerNode attackNode;
        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if(groundChecker.IsGrounded) {
                context.TryTransition(landNode);
            }
        }

        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
              context.TryTransition(attackNode);
        }

        public override void OnChargeRequested(IStrikerStateContext context) {
            context.TryTransition(attackNode);
        }

        // ダッシュコマンドが押された時に呼ばれる
        public override void OnDashRequested(IStrikerStateContext context) {
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
