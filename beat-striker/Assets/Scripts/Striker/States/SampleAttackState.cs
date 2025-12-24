using Core.Battle;
using Core.Striker.Components;
using UnityEngine;

namespace Core.Striker {

    public class SampleAttackState : StrikerState {
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode hit;

        public override void OnEnter(IStrikerContext hub) {
            hub.PlayAnimation(animationClip, OnAnimationComplete);
        }

        public override void OnUpdate(IStrikerStateContext hub) {
        }

        public override void OnExit(IStrikerContext hub) {
        }

        private void OnAnimationComplete(IStrikerStateContext hub) {
            hub.TryTransition();
        }

        public override void OnAttackRequested(IStrikerStateContext hub) {
        }

        public override void OnChargeRequested(IStrikerStateContext hub) {
        }

        public override void OnDashRequested(IStrikerStateContext hub) {
        }

        public override void OnGuardRequested(IStrikerStateContext hub) {
        }

        public override void OnHit(IStrikerStateContext hub, HitStatus status) {
            hub.TryTransition(hit);
        }

        public override void OnMiss(IStrikerStateContext hub) {
        }
    }
}
