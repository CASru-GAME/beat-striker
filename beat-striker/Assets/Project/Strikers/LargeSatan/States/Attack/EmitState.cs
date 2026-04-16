using UnityEngine;
using Alice;
using R3;
using System;

namespace Core.LargeSatan {



    public class EmitState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        [SerializeField] private StrikerAnimationClip animationClip, warpedAnimationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] private PoleArm poleArm;
        [SerializeField] float emitHoldDuration = 0.12f;
        [SerializeField] float emitSpeed = 12f;
        [SerializeField] float recoilSpeed = 12f;
        [SerializeField] AnimationCurve emitSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
        [SerializeField] float duration = 1f;
        [SerializeField, Min(0f)] float recoilStartDelay = 0.1f;
        [SerializeField, Min(0f)] float recoilEndSpeedRatio = 0.01f;
        [SerializeField] float damage = 10f;
        [SerializeField] float fallbackClearanceRadius = 0.5f;
        [SerializeField, Min(0f)] float minWarpDistanceFromOpponent = 0.5f;
        [SerializeField] Vector3 postAimHoldAdjustmentEulerAngles;
        [SerializeField, Min(0f)] float aimDirectionReuseThreshold = 15f;
        [SerializeField] EffectPlayer warpEffectPlayer, warpOutEffectPlayer;
        Quaternion finalRotation;
        Vector3 recoilDirection;
        float elapsedTime;
        bool isFinished;
        bool emitStoppedByWall;
        Vector3 emitStopPosition;

        public override void OnEnter(IStrikerContext context) {
            context.Rigidbody.useGravity = false;

            elapsedTime = 0f;
            isFinished = false;
            emitStoppedByWall = false;
            emitStopPosition = context.Rigidbody.position;

            poleArm.OnHitHurtbox.Subscribe(hurtbox => {
                hurtbox.GiveHit(new HitStatus(damage));
            }).AddTo(disposables);

            poleArm.OnHitWall.Subscribe(hit => {
                if (emitStoppedByWall) return;
                emitStoppedByWall = true;
                emitStopPosition = hit.pos;
            }).AddTo(disposables);

            var lookDirection = GetLookDirection(context);
            var inputDirection = (Vector3)context.InputDirection;
            var useStoredDirection = inputDirection.sqrMagnitude < 0.001f;

            var plannedEmitAngle = poleArm.PlannedEmitAngle;
            var chargeInputDirection = poleArm.ChargeInputDirection;
            var selectedAngle = plannedEmitAngle;

            if (!useStoredDirection) {
                var chargeInputAngle = ToRelativeAngle(chargeInputDirection, lookDirection);
                var inputAngle = ToRelativeAngle(inputDirection, lookDirection);
                var angleDelta = Mathf.Abs(Mathf.DeltaAngle(inputAngle, chargeInputAngle));
                selectedAngle = angleDelta <= aimDirectionReuseThreshold
                    ? plannedEmitAngle
                    : SnapAngleToNearestEightWay(inputAngle);
            }

            var isRearAngle = IsRearSnapAngle(selectedAngle);
            var animationFlipY = isRearAngle ? 180f : 0f;
            var animationAngleX = isRearAngle ? ConvertRearAngleToFrontAngle(selectedAngle) : selectedAngle;
            var emitAngle = selectedAngle;

            var throwDirection = ToWorldDirection(emitAngle, lookDirection);
            finalRotation = Quaternion.LookRotation(throwDirection);
            recoilDirection = -throwDirection.normalized;

            poleArm.BeginEmit(finalRotation, emitHoldDuration, emitSpeed, duration, emitSpeedCurve, postAimHoldAdjustmentEulerAngles);

            context.PlayAnimation(animationClip, Vector3.zero, new Vector3(animationAngleX, animationFlipY, 0f), _ => { });
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if (isFinished) {
                context.TryTransition(nextNode);
                return;
            }

            elapsedTime += Time.deltaTime;
            ApplyRecoilMovement(context);

            if (elapsedTime >= duration) {
                isFinished = true;
                context.TryTransition(nextNode);
            }
        }

        public override void OnExit(IStrikerContext context) {
            context.PlayAnimation(warpedAnimationClip);
            context.Rigidbody.linearVelocity = Vector3.zero;
            var warpPosition = ComputeSafeWarpPosition(context);
            context.Rigidbody.position = warpPosition;
            context.Rigidbody.rotation = ComputeFacingRotationTowardsOpponent(context, warpPosition);
            poleArm.RequestEndEmit();
            context.Rigidbody.useGravity = true;

            warpEffectPlayer.Emit(warpEffectPlayer.transform);
            warpOutEffectPlayer.Emit(warpOutEffectPlayer.transform);
        }

        void ApplyRecoilMovement(IStrikerStateContext context) {
            if (elapsedTime <= recoilStartDelay) {
                return;
            }

            var recoilDuration = Mathf.Max(duration - recoilStartDelay, 0f);
            if (recoilDuration <= 0f) {
                return;
            }

            var moveElapsedTime = Mathf.Min(elapsedTime - recoilStartDelay, recoilDuration);
            if (moveElapsedTime <= 0f) {
                return;
            }

            var decayRate = Mathf.Log(1f / Mathf.Max(recoilEndSpeedRatio, 0.0001f)) / Mathf.Max(recoilDuration, 0.0001f);
            var currentSpeed = recoilSpeed * Mathf.Exp(-decayRate * moveElapsedTime);
            var moveDistance = currentSpeed * Time.deltaTime;

            if (moveDistance <= 0f) {
                return;
            }

            context.Rigidbody.position += recoilDirection * moveDistance;
        }

        Vector3 ComputeSafeWarpPosition(IStrikerContext context) {
            Vector3 currentPos = context.Rigidbody.position;
            Vector3 targetPos = emitStoppedByWall ? emitStopPosition : poleArm.transform.position;
            Vector3 dir = targetPos - currentPos;
            float dist = dir.magnitude;

            if (dist <= 0.01f) {
                return currentPos;
            }

            var dirNormalized = dir / dist;

            float radius = fallbackClearanceRadius;
            float heightOffset = 0f;
            if (context.Rigidbody.TryGetComponent<CapsuleCollider>(out var capsule)) {
                radius = capsule.radius;
                heightOffset = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            }

            var safeDist = ComputeWallSafeDistance(context, currentPos, dirNormalized, dist, radius, heightOffset);
            if (safeDist <= 0.01f) {
                return currentPos;
            }

            safeDist = ClampDistanceByOpponent(currentPos, dirNormalized, safeDist, context.GetOpponent().CenterPosition.CurrentValue, minWarpDistanceFromOpponent);
            safeDist = ComputeWallSafeDistance(context, currentPos, dirNormalized, safeDist, radius, heightOffset);

            return currentPos + dirNormalized * safeDist;
        }

        float ComputeWallSafeDistance(IStrikerContext context, Vector3 startPos, Vector3 moveDirection, float maxDistance, float radius, float heightOffset) {
            if (maxDistance <= 0f) {
                return 0f;
            }

            float safeDist = maxDistance;

            if (context.Rigidbody.TryGetComponent<CapsuleCollider>(out var cap)) {
                Vector3 startCenter = startPos + cap.center;
                Vector3 p1 = startCenter + Vector3.up * heightOffset;
                Vector3 p2 = startCenter - Vector3.up * heightOffset;

                if (Physics.CapsuleCast(p1, p2, radius, moveDirection, out var hit, maxDistance, poleArm.wallMask, QueryTriggerInteraction.Ignore)) {
                    safeDist = hit.distance - 0.02f;
                }
            }
            else {
                Vector3 castStart = startPos + Vector3.up * radius;
                if (Physics.SphereCast(castStart, radius, moveDirection, out var hit, maxDistance, poleArm.wallMask, QueryTriggerInteraction.Ignore)) {
                    safeDist = hit.distance - 0.02f;
                }
            }

            return Mathf.Max(0f, safeDist);
        }

        static float ClampDistanceByOpponent(Vector3 startPos, Vector3 moveDirection, float desiredDistance, Vector3 opponentPos, float minDistance) {
            if (minDistance <= 0f || desiredDistance <= 0f) {
                return Mathf.Max(0f, desiredDistance);
            }

            var minDistanceSq = minDistance * minDistance;
            var endPos = startPos + moveDirection * desiredDistance;

            if ((endPos - opponentPos).sqrMagnitude >= minDistanceSq) {
                return desiredDistance;
            }

            var startToOpponent = startPos - opponentPos;
            var startDistanceSq = startToOpponent.sqrMagnitude;
            if (startDistanceSq <= minDistanceSq) {
                return 0f;
            }

            var b = 2f * Vector3.Dot(startToOpponent, moveDirection);
            var c = startDistanceSq - minDistanceSq;
            var discriminant = b * b - 4f * c;

            if (discriminant <= 0f) {
                return 0f;
            }

            var sqrtDiscriminant = Mathf.Sqrt(discriminant);
            var t1 = (-b - sqrtDiscriminant) * 0.5f;
            var t2 = (-b + sqrtDiscriminant) * 0.5f;
            var enter = Mathf.Min(t1, t2);
            var exit = Mathf.Max(t1, t2);

            if (desiredDistance <= enter || desiredDistance >= exit) {
                return desiredDistance;
            }

            return Mathf.Max(0f, enter - 0.001f);
        }

        Quaternion ComputeFacingRotationTowardsOpponent(IStrikerContext context, Vector3 warpPosition) {
            var opponent = context.GetOpponent();
            var direction = opponent.CenterPosition.CurrentValue - warpPosition;

            return Quaternion.LookRotation(Mathf.Sign(direction.x) * Vector3.right, Vector3.up);
        }

        static Vector3 GetLookDirection(IStrikerContext context) {
            var lookDirection = context.GetSelf().LookDirection.CurrentValue;
            if (lookDirection.sqrMagnitude > 0.0001f) {
                return lookDirection.normalized;
            }

            var fallback = context.Rigidbody.transform.right;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.right;
        }

        static float ToRelativeAngle(Vector3 worldDirection, Vector3 lookDirection) {
            var normalizedWorldDirection = worldDirection.normalized;
            var normalizedLookDirection = lookDirection.normalized;
            var lookSign = Mathf.Abs(normalizedLookDirection.x) < 0.0001f ? 1f : Mathf.Sign(normalizedLookDirection.x);
            var signed = Vector3.SignedAngle(normalizedLookDirection, normalizedWorldDirection, Vector3.forward);

            return -signed * lookSign;
        }

        static Vector3 ToWorldDirection(float relativeAngle, Vector3 lookDirection) {
            var normalizedLookDirection = lookDirection.normalized;
            var lookSign = Mathf.Abs(normalizedLookDirection.x) < 0.0001f ? 1f : Mathf.Sign(normalizedLookDirection.x);
            var worldRotation = -relativeAngle * lookSign;
            var rotated = Quaternion.AngleAxis(worldRotation, Vector3.forward) * normalizedLookDirection;

            return rotated.normalized;
        }

        static float SnapAngleToNearestEightWay(float angle) {
            float[] snapCandidates = { -90f, -45f, 0f, 45f, 90f, 135f, 180f, 225f };
            var best = snapCandidates[0];
            var minDelta = Mathf.Abs(Mathf.DeltaAngle(angle, best));

            for (int i = 1; i < snapCandidates.Length; i++) {
                var candidate = snapCandidates[i];
                var delta = Mathf.Abs(Mathf.DeltaAngle(angle, candidate));
                if (delta < minDelta) {
                    minDelta = delta;
                    best = candidate;
                }
            }

            return best;
        }

        static bool IsRearSnapAngle(float angle) {
            return IsNearAngle(angle, 135f) || IsNearAngle(angle, 180f) || IsNearAngle(angle, 225f);
        }

        static float ConvertRearAngleToFrontAngle(float angle) {
            if (IsNearAngle(angle, 135f)) {
                return 45f;
            }

            if (IsNearAngle(angle, 180f)) {
                return 0f;
            }

            if (IsNearAngle(angle, 225f)) {
                return -45f;
            }

            return angle;
        }

        static bool IsNearAngle(float angle, float target) {
            return Mathf.Abs(Mathf.DeltaAngle(angle, target)) <= 0.001f;
        }
    }
}


