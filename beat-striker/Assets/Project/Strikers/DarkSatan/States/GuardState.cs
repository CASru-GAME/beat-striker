using UnityEngine;
using Alice;

namespace Core.DarkSatan {



    public class GuardState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Guard;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;

        [SerializeField] Collider mainCollider;
        [SerializeField] GameObject shield;
        [SerializeField] AudioClip shieldSound;
        [SerializeField] LayerMask wallMask = Physics.DefaultRaycastLayers;

        [SerializeField, Min(0f)] float guardShiftDistance = 0.2f;
        [SerializeField, Min(0f)] float fallbackClearanceRadius = 0.5f;
        [SerializeField, Min(0f)] float minDistanceFromOpponent = 0.5f;

        public float ObservationShift => guardShiftDistance;

        public override void OnEnter(IStrikerContext context) {
            context.Rigidbody.useGravity = false;
            context.Rigidbody.linearVelocity = Vector3.zero;
            ApplySafeGuardShift(context);
            ScheduleStateEvent(context.BeginRecoveringGuard(), nextContext => {
                nextContext.TryTransition(nextNode);
            });
            shield.SetActive(true);
            shieldSound.PlayAtApp(context.Rigidbody.position);
            mainCollider.enabled = false;
        }

        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.useGravity = true;
            shield.SetActive(false);
            mainCollider.enabled = true;
        }


        public override void OnHit(IStrikerStateContext context, HitStatus status) {
            context.PreventGroup();
        }

        void ApplySafeGuardShift(IStrikerContext context) {
            var inputDirection = (Vector3)context.InputDirection;
            if (inputDirection.sqrMagnitude <= 0.0001f) {
                return;
            }

            var startPos = context.Rigidbody.position;
            var shiftDelta = inputDirection.normalized * guardShiftDistance;
            var resolvedWallMask = wallMask.value == 0 ? (LayerMask)Physics.DefaultRaycastLayers : wallMask;
            var safeDestination = StrikerWarpSafetyUtility.ComputeSafeLinearMoveDestination(
                context,
                startPos,
                shiftDelta,
                resolvedWallMask,
                fallbackClearanceRadius,
                minDistanceFromOpponent
            );

            context.Rigidbody.position = safeDestination;
        }

    }
}


