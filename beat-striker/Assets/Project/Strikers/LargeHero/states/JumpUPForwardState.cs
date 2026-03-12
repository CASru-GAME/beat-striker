using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class JumpUPForwardState : StrikerState {
        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode fallNode;
        [SerializeField] StrikerNode attackNode;
        [SerializeField] StrikerNode chargeNode;
        [SerializeField] float jumpSpeed;
        [SerializeField] float linearDamping;
        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
        
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);
            context.Rigidbody.linearVelocity = jumpSpeed * context.InputDirection;
            context.Rigidbody.linearDamping = linearDamping;
        }
    


        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.linearDamping = 0f;
        }
        
        void OnAnimationEnd(IStrikerStateContext context){
            context.TryTransition(fallNode);
        }
        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            context.TryTransition(attackNode);
        }

        // チャージコマンドが押された時に呼ばれる
        public override void OnChargeRequested(IStrikerStateContext context) {
            context.TryTransition(chargeNode);
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
