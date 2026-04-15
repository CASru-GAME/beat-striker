using UnityEngine;
using Alice;

namespace Core.LargeSatan {



    public class ChargeState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Charge;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode, emitNode;
        [SerializeField] float speed = 1f;
        [SerializeField] private PoleArm poleArm;
        [SerializeField] float aimSpinDuration = 0.35f;
        [SerializeField] float aimSpinCount = 1f;
        [SerializeField] Transform aimSpinAxisStart;
        [SerializeField] Transform aimSpinAxisEnd;
        [SerializeField, Min(0f)] float aimRotationSmooth = 16f;
        [SerializeField] float aimSpinSpearLocalGripOffsetZ = -1f;
        [SerializeField] Vector3 aimHandRotationAdjustmentEulerAngles = new Vector3(0f, 180f, 0f);
        Vector3 initialSpeed;
        bool isTransitioningToEmitState;

        public override void OnEnter(IStrikerContext context) {
            initialSpeed = speed * context.InputDirection.x * Vector3.right;
            isTransitioningToEmitState = false;
            poleArm.BeginAim(
                aimSpinDuration,
                aimSpinCount,
                aimSpinAxisStart,
                aimSpinAxisEnd,
                context.InputDirection.x,
                aimRotationSmooth,
                aimSpinSpearLocalGripOffsetZ,
                aimHandRotationAdjustmentEulerAngles);

            context.PlayAnimation(animationClip, context => {
                context.TryTransition(nextNode);
            });
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.Rigidbody.linearVelocity = initialSpeed;
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.linearVelocity = Vector3.zero;
            if (!isTransitioningToEmitState) {
                poleArm.RequestEndEmit();
            }
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
            context.PreventGroup();
            isTransitioningToEmitState = true;
            context.TryTransition(emitNode);
        }

    }
}


