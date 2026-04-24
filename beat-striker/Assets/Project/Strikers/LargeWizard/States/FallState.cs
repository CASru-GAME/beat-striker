using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {



    public class FallState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] GroundChecker groundChecker;
        [SerializeField] StrikerNode landNode, locomotionNode;

        public override void OnEnter(IStrikerContext context) {

            context.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if (groundChecker.IsGrounded) {
                context.TryTransition(landNode);
            }
            else context.TryTransition(locomotionNode);
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


