using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using Core.Striker.Components;

namespace Core.LargeHero {
    
    public class RushState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] AttackPlayer attackPlayer;

        [SerializeField] float rushSpeed = 10f;
        float initialDirection;


        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip, OnAnimationEnd);

            initialDirection = Mathf.Sign(context.InputDirection.x);
            var v = context.Rigidbody.linearVelocity;
            v.x = initialDirection * rushSpeed;
            context.Rigidbody.linearVelocity = v;

            attackPlayer.Emit();
        }

        public override void OnUpdate(IStrikerStateContext context) {
            var v = context.Rigidbody.linearVelocity;
            v.x = initialDirection * rushSpeed;
            context.Rigidbody.linearVelocity = v;
        }

        void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

    }
}
