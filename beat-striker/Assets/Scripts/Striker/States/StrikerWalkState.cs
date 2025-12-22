using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    [AddComponentMenu(" StrikerStates/Walk State")]
    public class StrikerWalkState : StrikerState {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float rotationSpeed = 360f;
        private float? targetRotationAngle = null;

        private Components.StrikerGroundCheck groundCheck;

        private void Awake() {
            groundCheck = GetComponent<Components.StrikerGroundCheck>();
        }

        public override void Enter(IStrikerHub hub) {
            if (animationClip != null) hub.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerHub hub) {
            if (groundCheck != null && !groundCheck.IsGround) return;
            
            // Movement logic
            var direction = hub.InputDirection;
            
            if (direction != Vector2.zero && Mathf.Abs(hub.Rigidbody.linearVelocity.x) < walkSpeed && !targetRotationAngle.HasValue) {
                var v = hub.Rigidbody.linearVelocity;
                v.x = walkSpeed * direction.x;
                hub.Rigidbody.linearVelocity = v;
            }

            RotateTowardsDirection(direction);
        }

        private void RotateTowardsDirection(Vector2 targetDirection) {
             if (targetDirection.x != 0) {
                 targetRotationAngle = targetDirection.x > 0 ? 90f : -90f;
             }
             if (!targetRotationAngle.HasValue) return;
 
             float currentAngle = transform.eulerAngles.y;
             float angleDifference = Mathf.DeltaAngle(currentAngle, targetRotationAngle.Value);
             float rotationThisFrame = rotationSpeed * Time.deltaTime;
 
             if (Mathf.Abs(angleDifference) < rotationThisFrame) {
                 transform.rotation = Quaternion.Euler(0, targetRotationAngle.Value, 0);
                 targetRotationAngle = null;
                 return;
             }
 
             float rotationAmount = Mathf.Clamp(angleDifference, -rotationThisFrame, rotationThisFrame);
             float newRotationAngle = currentAngle + rotationAmount;
             transform.rotation = Quaternion.Euler(0, newRotationAngle, 0);
        }

        public override void Exit() {
             base.Exit();
        }
    }
}
