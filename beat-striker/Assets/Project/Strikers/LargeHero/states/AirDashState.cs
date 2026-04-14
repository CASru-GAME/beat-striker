using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {


    public class AirDashState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;

        [SerializeField] private StrikerAnimationClip UpAnimationClip,FrontAnimationClip,BackAnimationClip;
        [SerializeField] private float speed;
        [SerializeField] StrikerNode nextNode;

        public override void OnEnter(IStrikerContext context) {
            context.Rigidbody.linearVelocity = context.InputDirection * speed;
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

        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        public override void OnChargeRequested(IStrikerStateContext context) {
        }

        public override void OnDashRequested(IStrikerStateContext context) {
        }

        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        public override void OnHit(IStrikerStateContext context, HitStatus status) {
        }

        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}


