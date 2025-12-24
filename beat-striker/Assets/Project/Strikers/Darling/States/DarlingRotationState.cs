using Core.Battle;
using UnityEngine;

namespace Core.Striker.Darling.States {
    [AddComponentMenu(" StrikerStates/Rotation State")]
    public class DarlingRotationState : StrikerState {
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private float rotationSpeed = 360f;
        private float? targetRotationAngle = null;

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
            var direction = hub.InputDirection;

            if (direction.x != 0) {
                targetRotationAngle = direction.x > 0 ? 90f : -90f;
            }
            if (!targetRotationAngle.HasValue){
                hub.TryTransition();
                return; 
            }

            float currentAngle = transform.eulerAngles.y;
            float angleDifference = Mathf.DeltaAngle(currentAngle, targetRotationAngle.Value);
            float rotationThisFrame = rotationSpeed * Time.deltaTime;

            if (Mathf.Abs(angleDifference) < rotationThisFrame) {
                transform.rotation = Quaternion.Euler(0, targetRotationAngle.Value, 0);
                targetRotationAngle = null;
                hub.TryTransition();
                return;
            }

            float rotationAmount = Mathf.Clamp(angleDifference, -rotationThisFrame, rotationThisFrame);
            float newRotationAngle = currentAngle + rotationAmount;
            transform.rotation = Quaternion.Euler(0, newRotationAngle, 0);
        }
    }
}