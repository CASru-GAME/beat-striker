using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class AirDashState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip UpAnimationClip,FrontAnimationClip,BackAnimationClip;
        [SerializeField] private float speed;
        [SerializeField] StrikerNode nextNode;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            context.Rigidbody.linearVelocity = context.InputDirection * speed;
            // アニメーションの再生を開始する
                StrikerAnimationClip animationClip;
                if (context.LocalInputDirection.y > 0.5f) {
                    animationClip = UpAnimationClip;
                } else if (context.LocalInputDirection.y < -0.5f) {
                    animationClip = BackAnimationClip;
                } else {
                    animationClip = FrontAnimationClip;
                }
                
            context.PlayAnimation(animationClip,context => context.TryTransition(nextNode));
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        // チャージコマンドが押された時に呼ばれる
        public override void OnChargeRequested(IStrikerStateContext context) {
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

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
