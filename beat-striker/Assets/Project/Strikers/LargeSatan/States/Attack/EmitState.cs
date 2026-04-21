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
        [SerializeField] float recoilSpeed = 12f;
        [SerializeField] AnimationCurve emitSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
        [SerializeField] float duration = 1f;
        [SerializeField, Min(0f)] float recoilStartDelay = 0.1f;
        [SerializeField, Min(0f)] float recoilEndSpeedRatio = 0.01f;
        [SerializeField] float damage = 10f, knockbackSpeed = 5f, impact = 5f;
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
        bool hasWarped;

        public override void OnEnter(IStrikerContext context) {
            context.Rigidbody.useGravity = false;

            elapsedTime = 0f;
            isFinished = false;
            emitStoppedByWall = false;
            emitStopPosition = context.Rigidbody.position;
            hasWarped = false;

            poleArm.OnHitHurtbox.Subscribe(hurtbox => {
                hurtbox.GiveHit(new HitStatus(damage, knockbackSpeed * poleArm.transform.forward));
                context.GenerateImpact(new StrikerImpact(impact * Vector3.up));
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
            context.Rigidbody.linearVelocity = Vector3.zero;
            WarpIfNeeded(context);
            poleArm.RequestEndEmit();
            context.Rigidbody.useGravity = true;
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
            WarpIfNeeded(context);
        }
        
        public override void OnChargeRequested(IStrikerStateContext context) {
            WarpIfNeeded(context);
        }

        public override void OnDashRequested(IStrikerStateContext context) {
            WarpIfNeeded(context);
        }

        public override void OnGuardRequested(IStrikerStateContext context) {
            WarpIfNeeded(context);
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

        void WarpIfNeeded(IStrikerContext context) {
            if (hasWarped) {
                return;
            }

            var warpPosition = ComputeSafeWarpPosition(context);
            context.Rigidbody.position = warpPosition;
            context.Rigidbody.rotation = ComputeFacingRotationTowardsOpponent(context, warpPosition);
            warpEffectPlayer.Emit(warpEffectPlayer.transform);
            warpOutEffectPlayer.Emit(warpOutEffectPlayer.transform);
            hasWarped = true;
        }

        Vector3 ComputeSafeWarpPosition(IStrikerContext context) {
            Vector3 currentPos = context.Rigidbody.position;
            Vector3 targetPos = emitStoppedByWall ? emitStopPosition : poleArm.transform.position;
            return StrikerWarpSafetyUtility.ComputeSafeDestination(
                context,
                currentPos,
                targetPos,
                poleArm.wallMask,
                fallbackClearanceRadius,
                minWarpDistanceFromOpponent
            );
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


