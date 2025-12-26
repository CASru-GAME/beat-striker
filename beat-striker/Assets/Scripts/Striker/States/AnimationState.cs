using Core.Battle;
using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu(" StrikerStates/Animation State")]
    public class AnimationState : StrikerState
    {
        [SerializeField] private StrikerAnimationClip animationClip;

        public override void OnAttackRequested(IStrikerStateContext hub) {
        }

        public override void OnChargeRequested(IStrikerStateContext hub) {
        }

        public override void OnDashRequested(IStrikerStateContext hub) {
        }

        public override void OnEnter(IStrikerContext hub) {
            hub.PlayAnimation(animationClip);
        }

        public override void OnExit(IStrikerContext hub) {
        }

        public override void OnGuardRequested(IStrikerStateContext hub) {
        }

        public override void OnHit(IStrikerStateContext hub, HitStatus status) {
        }

        public override void OnMiss(IStrikerStateContext hub) {
        }

        public override void OnUpdate(IStrikerStateContext hub) {
        }
    }
}