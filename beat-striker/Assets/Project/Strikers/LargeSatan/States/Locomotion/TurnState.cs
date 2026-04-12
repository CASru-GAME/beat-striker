using UnityEngine;
using Alice;

namespace Core.LargeSatan {


    public class TurnState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Idle;
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] float duration = 0.2f;

        const float COMPLETION_ANGLE_EPSILON = 0.01f;

        Quaternion targetRotation;
        RigidbodyConstraints originalConstraints;
        bool turnCompleted;

        public override void OnEnter(IStrikerContext context) {
            originalConstraints = context.Rigidbody.constraints;
            context.Rigidbody.constraints = originalConstraints & ~RigidbodyConstraints.FreezeRotationY;

            var startRotation = context.Rigidbody.rotation;
            targetRotation = startRotation * Quaternion.Euler(0f, 180f, 0f);
            turnCompleted = false;
            context.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if (turnCompleted) {
                return;
            }

            var turnSpeed = duration <= 0f ? 3600f : 180f / duration;
            var nextRotation = Quaternion.RotateTowards(context.Rigidbody.rotation, targetRotation, turnSpeed * Time.deltaTime);
            context.Rigidbody.MoveRotation(nextRotation);

            if (Quaternion.Angle(nextRotation, targetRotation) > COMPLETION_ANGLE_EPSILON) {
                return;
            }

            context.Rigidbody.MoveRotation(targetRotation);
            turnCompleted = true;
            context.TryTransition(nextNode);
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.constraints = originalConstraints;
        }
    }
}


