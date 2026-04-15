using UnityEngine;
using Alice;

namespace Core.LargeSatan {



    public class EmitState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] private PoleArm poleArm;
        [SerializeField] float emitHoldDuration = 0.12f;
        [SerializeField] float emitSpeed = 12f;
        [SerializeField] Vector3 emitRotationOffsetEulerAngles;
        [SerializeField] Vector3 postAimHoldAdjustmentEulerAngles;
        [SerializeField] float speed = 1f;
        Vector3 initialSpeed;
        Quaternion finalRotation;

        public override void OnEnter(IStrikerContext context) {
            initialSpeed = speed * context.InputDirection.x * Vector3.right;
            var inputX = context.InputDirection.x;
            finalRotation = Mathf.Approximately(inputX, 0f)
                ? context.Rigidbody.rotation
                : Quaternion.Euler(0f, inputX < 0f ? 180f : 0f, 0f);
            finalRotation *= Quaternion.Euler(emitRotationOffsetEulerAngles);

            poleArm.BeginEmit(finalRotation, emitHoldDuration, emitSpeed, postAimHoldAdjustmentEulerAngles);

            context.PlayAnimation(animationClip, context => {
                context.TryTransition(nextNode);
            });
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.Rigidbody.linearVelocity = initialSpeed;
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.linearVelocity = Vector3.zero;
            poleArm.RequestEndEmit();
        }
    }
}


