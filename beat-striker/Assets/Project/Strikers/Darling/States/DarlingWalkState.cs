using Core.Battle;
using UnityEngine;

namespace Core.Striker.Darling.States {
    [AddComponentMenu(" StrikerStates/Walk State")]
    public class DarlingWalkState : StrikerState {
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private float walkSpeed = 5f;
    
        [SerializeField] private StrikerNode locomotion,attack,hit,charge;

        public override void OnEnter(IStrikerContext hub) {
            hub.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext hub) {
            var direction = hub.InputDirection;
            
            if (direction != Vector2.zero && Mathf.Abs(hub.Rigidbody.linearVelocity.x) < walkSpeed) {
                var v = hub.Rigidbody.linearVelocity;
                v.x = walkSpeed * direction.x;
                hub.Rigidbody.linearVelocity = v;
            }

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
        }

        public override void OnGuardRequested(IStrikerStateContext hub) {
        }

        public override void OnHit(IStrikerStateContext hub, HitStatus status) {
        }

        public override void OnMiss(IStrikerStateContext hub) {
        }
    }
}
