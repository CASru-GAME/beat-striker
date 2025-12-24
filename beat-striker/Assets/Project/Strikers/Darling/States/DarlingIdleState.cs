using Core.Battle;
using Core.Striker.Components;
using UnityEngine;

namespace Core.Striker.Darling.States {

    public class DarlingIdleState : StrikerState {
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode locomotion,attack,dash,hit,charge;

        public override void OnEnter(IStrikerContext hub) {
            hub.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext hub) {
            hub.TryTransition(locomotion);
        }

        public override void OnExit(IStrikerContext hub) {
        }

        public override void OnAttackRequested(IStrikerStateContext hub) {
            hub.TryTransition(attack);
        }

        public override void OnChargeRequested(IStrikerStateContext hub) {
            hub.TryTransition(charge);
        }

        public override void OnDashRequested(IStrikerStateContext hub) {
            hub.TryTransition(dash);
        }

        public override void OnGuardRequested(IStrikerStateContext hub) {
        }

        public override void OnHit(IStrikerStateContext hub, HitStatus status) {
        }

        public override void OnMiss(IStrikerStateContext hub) {
        }
    }
}
