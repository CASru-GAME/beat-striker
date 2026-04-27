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
        [SerializeField] Vector3 aimOpponentOffset = new Vector3(0f, 1f, 0f);
        Vector3 initialSpeed;
        bool isTransitioningToEmitState;

        public override void OnEnter(IStrikerContext context) {
            var toOpponent = context.GetOpponent().Position.CurrentValue - context.Rigidbody.position;
            if (Vector3.Dot(context.Rigidbody.transform.forward, toOpponent) < 0) {
                context.Rigidbody.rotation *= Quaternion.Euler(0, 180, 0);
            }

            context.Rigidbody.useGravity = false;

            initialSpeed = speed * context.InputDirection.x * Vector3.right;
            isTransitioningToEmitState = false;

            ScheduleStateEvent(0.1f, ctx => {
                var animationRotationOffset = CalcAnimationRotationOffset(ctx);

                poleArm.BeginAim(
                    aimSpinDuration,
                    aimSpinCount,
                    aimSpinAxisStart,
                    aimSpinAxisEnd,
                    ctx.InputDirection.x,
                    aimRotationSmooth,
                    aimSpinSpearLocalGripOffsetZ,
                    animationRotationOffset.x,
                    ctx.InputDirection,
                    aimHandRotationAdjustmentEulerAngles);

                ctx.PlayAnimation(animationClip, Vector3.zero, animationRotationOffset, OnAnimationEnd);
            });
        }

        void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        Vector3 CalcAnimationRotationOffset(IStrikerContext context) {
            var opponentDirection = (context.GetOpponent().Position.CurrentValue + aimOpponentOffset - context.Rigidbody.position).normalized;
            var lookDirection = context.GetSelf().LookDirection.CurrentValue;
            var cos = Vector3.Dot(lookDirection, opponentDirection);
            var sin = Vector3.Cross(opponentDirection, lookDirection).z * Mathf.Sign(lookDirection.x);

            return opponentDirection != Vector3.zero && cos > 0
                ? new Vector3(Mathf.Atan2(sin, cos) * Mathf.Rad2Deg, 0f, 0f)
                : Vector3.zero;
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.Rigidbody.linearVelocity = initialSpeed;
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.linearVelocity = Vector3.zero;
            if (!isTransitioningToEmitState) {
                poleArm.RequestEndEmit();
            }

            context.Rigidbody.useGravity = true;
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
            context.PreventGroup();
            isTransitioningToEmitState = true;
            context.TryTransition(emitNode);
        }

    }
}


