using UnityEngine;

[RequireComponent(typeof(Tracker))]
public class PoleArm : MonoBehaviour {
    Tracker tracker;
    [SerializeField] Transform hand;
    IState currentState;
    Tracker.TargetHandle baseTargetHandle;
    Vector3 neutralRelativePosition;
    Quaternion neutralRelativeRotation;
    bool hasNeutralPose;

    public void Awake() {
        TryGetComponent(out tracker);
    }

    public void Start() {
        ChangeState(new DefaultState(this));
    }

    public void Update() {
        currentState?.OnUpdate(Time.deltaTime);
    }

    public void BeginAim(float spinDuration, float spinCount, Transform spinAxisStart, Transform spinAxisEnd, float spinDirection, float rotationSmooth, float spearLocalGripOffsetZ, Vector3 aimHandRotationAdjustmentEulerAngles) {
        EnsureBaseTarget();
        ChangeState(new AimState(this, spinDuration, spinCount, spinAxisStart, spinAxisEnd, spinDirection, rotationSmooth, spearLocalGripOffsetZ, aimHandRotationAdjustmentEulerAngles));
    }

    public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, Vector3 postAimHoldAdjustmentEulerAngles) {
        EnsureBaseTarget();
        currentState?.BeginEmit(finalRotation, holdDuration, speed, postAimHoldAdjustmentEulerAngles);
    }

    public void RequestEndEmit() {
        currentState?.EndEmit();
    }

    void EnsureNeutralPose() {
        if (hasNeutralPose) {
            return;
        }

        neutralRelativePosition = hand.InverseTransformPoint(transform.position);
        neutralRelativeRotation = Quaternion.Inverse(hand.rotation) * transform.rotation;
        hasNeutralPose = true;
    }

    void EnsureBaseTarget() {
        if (baseTargetHandle != null) {
            return;
        }

        RebindBaseTargetToNeutral();
    }

    void RebindBaseTargetToNeutral() {
        EnsureNeutralPose();

        if (baseTargetHandle != null) {
            tracker.RemoveTarget(baseTargetHandle);
            baseTargetHandle = null;
        }

        baseTargetHandle = tracker.AddTarget(hand, neutralRelativePosition, neutralRelativeRotation, followPosition: true, followRotation: true);
    }

    private void ChangeState(IState newState) {
        currentState?.OnExit();
        currentState = newState;
        currentState?.OnEnter();
    }

    Quaternion ApplyPostAimHoldFlip(Quaternion rotation, Vector3 postAimHoldAdjustmentEulerAngles) {
        // Always invert throw-facing so spear orientation matches throw direction.
        var inversion = Quaternion.AngleAxis(180f, Vector3.up);
        return rotation * inversion * Quaternion.Euler(postAimHoldAdjustmentEulerAngles);
    }

    class DefaultState : IState {
        readonly PoleArm poleArm;

        public DefaultState(PoleArm poleArm) {
            this.poleArm = poleArm;
        }

        public void OnEnter() {
            poleArm.RebindBaseTargetToNeutral();
        }

        public void OnExit() {
        }

        public void OnUpdate(float deltaTime) {
        }

        public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, Vector3 postAimHoldAdjustmentEulerAngles) {
            poleArm.ChangeState(new EmitPrepareState(poleArm, finalRotation, holdDuration, speed, postAimHoldAdjustmentEulerAngles));
        }

        public void EndEmit() {
        }
    }

    class AimState : IState {
        readonly PoleArm poleArm;
        readonly float spinDuration;
        readonly float spinCount;
        readonly Transform spinAxisStart;
        readonly Transform spinAxisEnd;
        readonly float spinDirection;
        readonly float rotationSmooth;
        readonly float spearLocalGripOffsetZ;
        readonly Quaternion aimHandRotationAdjustment;
        Vector3 lastResolvedAxisLocal;
        Vector3 basePerpendicularRightWorld;
        Vector3 gripPointLocal;
        float elapsedTime;
        bool isTrackingHand;
        bool hasPendingEmit;
        Quaternion pendingFinalRotation;
        float pendingHoldDuration;
        float pendingSpeed;
        Vector3 pendingPostAimHoldAdjustmentEulerAngles;
        Tracker.TargetHandle pauseTargetHandle;
        Tracker.TargetHandle holdTargetHandle;

        public AimState(PoleArm poleArm, float spinDuration, float spinCount, Transform spinAxisStart, Transform spinAxisEnd, float spinDirection, float rotationSmooth, float spearLocalGripOffsetZ, Vector3 aimHandRotationAdjustmentEulerAngles) {
            this.poleArm = poleArm;
            this.spinDuration = spinDuration;
            this.spinCount = spinCount;
            this.spinAxisStart = spinAxisStart;
            this.spinAxisEnd = spinAxisEnd;
            this.spinDirection = Mathf.Abs(spinDirection) < 0.0001f ? 1f : Mathf.Sign(spinDirection);
            this.rotationSmooth = rotationSmooth;
            this.spearLocalGripOffsetZ = spearLocalGripOffsetZ;
            this.aimHandRotationAdjustment = Quaternion.Euler(aimHandRotationAdjustmentEulerAngles);
        }

        public void OnEnter() {
            elapsedTime = 0f;
            isTrackingHand = false;
            hasPendingEmit = false;
            pauseTargetHandle = poleArm.tracker.AddTarget();
            holdTargetHandle = null;
            lastResolvedAxisLocal = Vector3.up;
            gripPointLocal = Vector3.forward * spearLocalGripOffsetZ;

            var axisWorld = ResolveAxisWorld();
            var projectedRight = Vector3.ProjectOnPlane(poleArm.transform.right, axisWorld);
            if (projectedRight.sqrMagnitude <= 0.000001f) {
                projectedRight = Vector3.ProjectOnPlane(poleArm.transform.up, axisWorld);
            }
            if (projectedRight.sqrMagnitude <= 0.000001f) {
                projectedRight = Vector3.Cross(axisWorld, poleArm.transform.forward);
            }
            basePerpendicularRightWorld = projectedRight.normalized;

            if (spinDuration <= 0f || spinCount <= 0f) {
                CompleteSpin();
            }
        }

        public void OnExit() {
            if (pauseTargetHandle != null) {
                poleArm.tracker.RemoveTarget(pauseTargetHandle);
                pauseTargetHandle = null;
            }

            if (holdTargetHandle != null) {
                poleArm.tracker.RemoveTarget(holdTargetHandle);
                holdTargetHandle = null;
            }
        }

        public void OnUpdate(float deltaTime) {
            if (isTrackingHand) {
                return;
            }

            elapsedTime += deltaTime;
            var duration = Mathf.Max(spinDuration, 0.0001f);
            var progress = Mathf.Clamp01(elapsedTime / duration);
            var angle = 360f * Mathf.Max(0f, spinCount) * progress * spinDirection;
            var axisWorld = ResolveAxisWorld();

            var targetRight = Quaternion.AngleAxis(angle, axisWorld) * basePerpendicularRightWorld;
            var perpendicularRight = Vector3.ProjectOnPlane(targetRight, axisWorld).normalized;
            var forward = Vector3.Cross(axisWorld, perpendicularRight).normalized;
            var targetRotation = Quaternion.LookRotation(forward, axisWorld) * aimHandRotationAdjustment;
            var smoothFactor = 1f - Mathf.Exp(-Mathf.Max(0f, rotationSmooth) * deltaTime);
            var smoothedRotation = Quaternion.Slerp(poleArm.transform.rotation, targetRotation, smoothFactor);
            var targetPosition = poleArm.hand.position - smoothedRotation * gripPointLocal;

            poleArm.transform.SetPositionAndRotation(targetPosition, smoothedRotation);

            if (progress >= 1f) {
                CompleteSpin();
            }
        }

        public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, Vector3 postAimHoldAdjustmentEulerAngles) {
            hasPendingEmit = true;
            pendingFinalRotation = finalRotation;
            pendingHoldDuration = holdDuration;
            pendingSpeed = speed;
            pendingPostAimHoldAdjustmentEulerAngles = postAimHoldAdjustmentEulerAngles;

            if (isTrackingHand) {
                poleArm.ChangeState(new EmitPrepareState(poleArm, pendingFinalRotation, pendingHoldDuration, pendingSpeed, postAimHoldAdjustmentEulerAngles));
            }
        }

        public void EndEmit() {
            poleArm.ChangeState(new DefaultState(poleArm));
        }

        void CompleteSpin() {
            if (isTrackingHand) {
                return;
            }

            isTrackingHand = true;

            if (pauseTargetHandle != null) {
                poleArm.tracker.RemoveTarget(pauseTargetHandle);
                pauseTargetHandle = null;
            }

            if (hasPendingEmit) {
                poleArm.ChangeState(new EmitPrepareState(poleArm, pendingFinalRotation, pendingHoldDuration, pendingSpeed, pendingPostAimHoldAdjustmentEulerAngles));
                return;
            }

            var holdRelativeRotation = poleArm.neutralRelativeRotation * aimHandRotationAdjustment;
            var holdRelativePosition = poleArm.neutralRelativePosition + (holdRelativeRotation * (Vector3.forward * spearLocalGripOffsetZ));
            holdTargetHandle = poleArm.tracker.AddTarget(
                poleArm.hand,
                holdRelativePosition,
                holdRelativeRotation,
                followPosition: true,
                followRotation: true);
        }

        Vector3 ResolveAxisWorld() {
            if (spinAxisStart == null || spinAxisEnd == null) {
                return poleArm.hand.up;
            }

            var axisWorld = spinAxisEnd.position - spinAxisStart.position;
            if (axisWorld.sqrMagnitude > 0.000001f) {
                lastResolvedAxisLocal = Quaternion.Inverse(poleArm.hand.rotation) * axisWorld.normalized;
            }

            return poleArm.hand.TransformDirection(lastResolvedAxisLocal).normalized;
        }
    }

    class EmitPrepareState : IState {
        readonly PoleArm poleArm;
        readonly Quaternion finalRotation;
        readonly float holdDuration;
        readonly float speed;
        readonly Vector3 postAimHoldAdjustmentEulerAngles;
        Quaternion holdRotation;
        float elapsedTime;
        Tracker.TargetHandle currentTargetHandle;

        public EmitPrepareState(PoleArm poleArm, Quaternion finalRotation, float holdDuration, float speed, Vector3 postAimHoldAdjustmentEulerAngles) {
            this.poleArm = poleArm;
            this.finalRotation = finalRotation;
            this.holdDuration = holdDuration;
            this.speed = speed;
            this.postAimHoldAdjustmentEulerAngles = postAimHoldAdjustmentEulerAngles;
        }

        public void OnEnter() {
            holdRotation = poleArm.ApplyPostAimHoldFlip(finalRotation, postAimHoldAdjustmentEulerAngles);
            poleArm.transform.rotation = holdRotation;
            elapsedTime = 0f;
            currentTargetHandle = poleArm.tracker.AddTarget(poleArm.hand, followPosition: true, followRotation: false);

            if (holdDuration <= 0f) {
                poleArm.ChangeState(new EmittionState(poleArm, holdRotation, speed));
            }
        }

        public void OnExit() {
            poleArm.tracker.RemoveTarget(currentTargetHandle);
            currentTargetHandle = null;
        }

        public void OnUpdate(float deltaTime) {
            elapsedTime += deltaTime;
            if (elapsedTime >= holdDuration) {
                poleArm.ChangeState(new EmittionState(poleArm, holdRotation, speed));
            }
        }

        public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, Vector3 postAimHoldAdjustmentEulerAngles) {
        }

        public void EndEmit() {
            poleArm.ChangeState(new DefaultState(poleArm));
        }
    }

    class EmittionState : IState {
        readonly PoleArm poleArm;
        readonly Quaternion finalRotation;
        readonly float speed;
        Vector3 emitDirection;
        Tracker.TargetHandle pauseTargetHandle;

        public EmittionState(PoleArm poleArm, Quaternion finalRotation, float speed) {
            this.poleArm = poleArm;
            this.finalRotation = finalRotation;
            this.speed = speed;
        }

        public void OnEnter() {
            pauseTargetHandle = poleArm.tracker.AddTarget();
            poleArm.transform.rotation = finalRotation;
            emitDirection = poleArm.transform.right;
        }

        public void OnExit() {
            poleArm.tracker.RemoveTarget(pauseTargetHandle);
            pauseTargetHandle = null;
        }

        public void OnUpdate(float deltaTime) {
            poleArm.transform.position += emitDirection * speed * deltaTime;
        }

        public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, Vector3 postAimHoldAdjustmentEulerAngles) {
        }

        public void EndEmit() {
            poleArm.ChangeState(new DefaultState(poleArm));
        }
    }

    private interface IState {
        void OnEnter();
        void OnExit();
        void OnUpdate(float deltaTime);
        void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, Vector3 postAimHoldAdjustmentEulerAngles);
        void EndEmit();
    }
}
