using UnityEngine;
using Alice;
using R3;
using System;

namespace Core.LargeSatan {



    public class EmitState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] private PoleArm poleArm;
        [SerializeField] float emitHoldDuration = 0.12f;
        [SerializeField] float emitSpeed = 12f;
        [SerializeField] AnimationCurve emitSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
        [SerializeField] float duration = 1f;
        [SerializeField] float damage = 10f;
        [SerializeField] float fallbackClearanceRadius = 0.5f;
        [SerializeField] Vector3 postAimHoldAdjustmentEulerAngles;
        Quaternion finalRotation;
        float elapsedTime;
        bool isFinished;

        public override void OnEnter(IStrikerContext context) {
            elapsedTime = 0f;
            isFinished = false;
            
            poleArm.OnHitHurtbox.Subscribe(hurtbox => {
                hurtbox.GiveHit(new HitStatus(damage));
            }).AddTo(disposables);
            
            poleArm.OnHitWall.Subscribe(_ => {
                if (isFinished) return;
                isFinished = true;
            }).AddTo(disposables);

            Vector3 throwDirection = context.InputDirection;
            if (throwDirection.sqrMagnitude < 0.001f) {
                throwDirection = context.Rigidbody.transform.forward;
            }

            finalRotation = Quaternion.LookRotation(throwDirection);

            poleArm.BeginEmit(finalRotation, emitHoldDuration, emitSpeed, duration, emitSpeedCurve, postAimHoldAdjustmentEulerAngles);

            context.PlayAnimation(animationClip, _ => {});
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if (isFinished) {
                context.TryTransition(nextNode);
                return;
            }

            elapsedTime += Time.deltaTime;

            if (elapsedTime >= duration) {
                isFinished = true;
                context.TryTransition(nextNode);
            }
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.linearVelocity = Vector3.zero;
            var warpPosition = ComputeSafeWarpPosition(context);
            context.Rigidbody.position = warpPosition;
            context.Rigidbody.rotation = ComputeFacingRotationTowardsOpponent(context, warpPosition);
            poleArm.RequestEndEmit();
        }

        Vector3 ComputeSafeWarpPosition(IStrikerContext context) {
            Vector3 currentPos = context.Rigidbody.position;
            Vector3 targetPos = poleArm.transform.position;
            Vector3 dir = targetPos - currentPos;
            float dist = dir.magnitude;

            if (dist <= 0.01f) {
                return currentPos;
            }

            float radius = fallbackClearanceRadius;
            float heightOffset = 0f;
            if (context.Rigidbody.TryGetComponent<CapsuleCollider>(out var capsule)) {
                radius = capsule.radius;
                heightOffset = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            }
            
            bool hitWall = false;
            float safeDist = dist;

            if (context.Rigidbody.TryGetComponent<CapsuleCollider>(out var cap)) {
                Vector3 startCenter = currentPos + cap.center;
                Vector3 p1 = startCenter + Vector3.up * heightOffset;
                Vector3 p2 = startCenter - Vector3.up * heightOffset;
                
                if (Physics.CapsuleCast(p1, p2, radius, dir.normalized, out var hit, dist, poleArm.wallMask, QueryTriggerInteraction.Ignore)) {
                    safeDist = hit.distance;
                    hitWall = true;
                }
            } else {
                Vector3 castStart = currentPos + Vector3.up * radius;
                if (Physics.SphereCast(castStart, radius, dir.normalized, out var hit, dist, poleArm.wallMask, QueryTriggerInteraction.Ignore)) {
                    safeDist = hit.distance;
                    hitWall = true;
                }
            }
            
            if (hitWall) {
                if (safeDist <= 0.01f) {
                    return currentPos;
                }
                return currentPos + dir.normalized * (safeDist - 0.02f);
            }

            return targetPos;
        }

        Quaternion ComputeFacingRotationTowardsOpponent(IStrikerContext context, Vector3 warpPosition) {
            var opponent = context.GetOpponent();
            var direction = opponent.CenterPosition.CurrentValue - warpPosition;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.000001f) {
                direction = context.Rigidbody.transform.forward;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.000001f) {
                direction = Vector3.forward;
            }

            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}


