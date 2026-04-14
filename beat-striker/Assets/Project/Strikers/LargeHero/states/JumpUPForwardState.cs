using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class JumpUPForwardState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode fallNode;
        [SerializeField] StrikerNode attackNode;
        [SerializeField] float jumpSpeed;
        
        public override void OnEnter(IStrikerContext context) {
        
            context.PlayAnimation(animationClip, OnAnimationEnd);
            var direction = context.InputDirection == Vector2.zero ? Vector2.up : context.InputDirection;
            context.Rigidbody.linearVelocity = jumpSpeed * direction;
            
        }
    


        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
            
        }
        
        void OnAnimationEnd(IStrikerStateContext context){
            context.TryTransition(fallNode);
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
