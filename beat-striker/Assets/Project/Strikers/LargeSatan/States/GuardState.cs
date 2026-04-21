using UnityEngine;
using Alice;
using R3;
using System;

namespace Core.LargeSatan {



    public class GuardState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Guard;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;

        [SerializeField] Collider mainCollider;
        [SerializeField] GameObject view;
        [SerializeField] EffectPlayer effectPlayer, endEffectPlayer;
        [SerializeField] LayerMask wallMask = Physics.DefaultRaycastLayers;

        [SerializeField] float guardDuration = 0.4f;
        [SerializeField, Min(0f)] float guardShiftDistance = 0.2f;
        [SerializeField, Min(0f)] float fallbackClearanceRadius = 0.5f;
        [SerializeField, Min(0f)] float minDistanceFromOpponent = 0.5f;

        public override void OnEnter(IStrikerContext context) {
            context.Rigidbody.useGravity = false;
            context.Rigidbody.linearVelocity = Vector3.zero;
            ApplySafeGuardShift(context);
            ScheduleStateEvent(guardDuration, nextContext => {
                nextContext.TryTransition(nextNode);
            });
            effectPlayer.Emit(effectPlayer.transform);
            view.SetActive(false);
            mainCollider.enabled = false;
        }

        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.useGravity = true;
            view.SetActive(true);
            mainCollider.enabled = true;
            endEffectPlayer.Emit(endEffectPlayer.transform);
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


