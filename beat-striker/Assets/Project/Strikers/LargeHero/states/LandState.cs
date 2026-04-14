using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {


    public class LandState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode locomotionNode;
        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip, OnAnimationEnd);
        }

        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
        }
        void OnAnimationEnd(IStrikerStateContext context){
            context.TryTransition(locomotionNode);
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


