using UnityEngine;
using R3;

[RequireComponent(typeof(Tracker))]
public class PoleArm : MonoBehaviour {
    Tracker tracker;
    [SerializeField] Transform hand;
    [SerializeField] Transform characterCenter;
    [SerializeField] public float hitRadius = 0.5f;
    [SerializeField] public LayerMask hitMask = Physics.DefaultRaycastLayers;
    [SerializeField] public LayerMask wallMask;
    [SerializeField, Min(0f)] float wallStickDepth = 0.08f;
    [SerializeField] ParticleSystem chargeEffect, emitEffect;
    [SerializeField] EffectPlayer hitEffectPlayer;
    readonly Subject<Hurtbox> onHitHurtbox = new();
    public Observable<Hurtbox> OnHitHurtbox => onHitHurtbox;
    
    readonly Subject<(Vector3 pos, Vector3 normal)> onHitWall = new();
    public Observable<(Vector3 pos, Vector3 normal)> OnHitWall => onHitWall;
    
    Transform originalParent;
    Transform ownerRoot;
    IState currentState;
    Tracker.TargetHandle baseTargetHandle;
    Vector3 neutralRelativePosition;
    Quaternion neutralRelativeRotation;
    bool hasNeutralPose;
    float plannedEmitAngle;
    Vector3 chargeInputDirection;

    public float PlannedEmitAngle => plannedEmitAngle;
    public Vector3 ChargeInputDirection => chargeInputDirection;

    public void Awake() {
        TryGetComponent(out tracker);
        originalParent = transform.parent;
        ownerRoot = transform.root;
    }

    public void Start() {
        ChangeState(new DefaultState(this));
    }

    public void Update() {
        currentState?.OnUpdate(Time.deltaTime);
    }

    public void BeginAim(float spinDuration, float spinCount, Transform spinAxisStart, Transform spinAxisEnd, float spinDirection, float rotationSmooth, float spearLocalGripOffsetZ, float plannedEmitAngle, Vector3 chargeInputDirection, Vector3 aimHandRotationAdjustmentEulerAngles) {
        EnsureBaseTarget();
        this.plannedEmitAngle = plannedEmitAngle;
        this.chargeInputDirection = chargeInputDirection;
        ChangeState(new AimState(this, spinDuration, spinCount, spinAxisStart, spinAxisEnd, spinDirection, rotationSmooth, spearLocalGripOffsetZ, aimHandRotationAdjustmentEulerAngles));
    }

    public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, float speedDuration, AnimationCurve speedCurve, Vector3 postAimHoldAdjustmentEulerAngles) {
        EnsureBaseTarget();
        currentState?.BeginEmit(finalRotation, holdDuration, speed, speedDuration, speedCurve, postAimHoldAdjustmentEulerAngles);
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
        return rotation * Quaternion.Euler(postAimHoldAdjustmentEulerAngles);
    }

    void PlayHitEffect(Vector3 position, Vector3 forwardDirection) {
        var forward = forwardDirection.sqrMagnitude > 0.000001f ? forwardDirection.normalized : Vector3.forward;
        hitEffectPlayer.Emit(position, Quaternion.LookRotation(forward), Vector3.one);
    }

    class DefaultState : IState {
        readonly PoleArm poleArm;

        public DefaultState(PoleArm poleArm) {
            this.poleArm = poleArm;
        }

        public void OnEnter() {
            poleArm.transform.SetParent(poleArm.originalParent);
            poleArm.RebindBaseTargetToNeutral();
        }

        public void OnExit() {
        }

        public void OnUpdate(float deltaTime) {
        }

        public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, float speedDuration, AnimationCurve speedCurve, Vector3 postAimHoldAdjustmentEulerAngles) {
            poleArm.ChangeState(new EmitPrepareState(poleArm, finalRotation, holdDuration, speed, speedDuration, speedCurve, postAimHoldAdjustmentEulerAngles));
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
        float pendingSpeedDuration;
        AnimationCurve pendingSpeedCurve;
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

        public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, float speedDuration, AnimationCurve speedCurve, Vector3 postAimHoldAdjustmentEulerAngles) {
            hasPendingEmit = true;
            pendingFinalRotation = finalRotation;
            pendingHoldDuration = holdDuration;
            pendingSpeed = speed;
            pendingSpeedDuration = speedDuration;
            pendingSpeedCurve = speedCurve;
            pendingPostAimHoldAdjustmentEulerAngles = postAimHoldAdjustmentEulerAngles;

            if (isTrackingHand) {
                poleArm.ChangeState(new EmitPrepareState(poleArm, pendingFinalRotation, pendingHoldDuration, pendingSpeed, pendingSpeedDuration, pendingSpeedCurve, postAimHoldAdjustmentEulerAngles));
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
                poleArm.ChangeState(new EmitPrepareState(poleArm, pendingFinalRotation, pendingHoldDuration, pendingSpeed, pendingSpeedDuration, pendingSpeedCurve, pendingPostAimHoldAdjustmentEulerAngles));
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


            poleArm.chargeEffect.Play();
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
        readonly float speedDuration;
        readonly AnimationCurve speedCurve;
        readonly Vector3 postAimHoldAdjustmentEulerAngles;
        Quaternion holdRotation;
        float elapsedTime;
        Tracker.TargetHandle currentTargetHandle;

        public EmitPrepareState(PoleArm poleArm, Quaternion finalRotation, float holdDuration, float speed, float speedDuration, AnimationCurve speedCurve, Vector3 postAimHoldAdjustmentEulerAngles) {
            this.poleArm = poleArm;
            this.finalRotation = finalRotation;
            this.holdDuration = holdDuration;
            this.speed = speed;
            this.speedDuration = speedDuration;
            this.speedCurve = speedCurve;
            this.postAimHoldAdjustmentEulerAngles = postAimHoldAdjustmentEulerAngles;
        }

        public void OnEnter() {
            holdRotation = poleArm.ApplyPostAimHoldFlip(finalRotation, postAimHoldAdjustmentEulerAngles);
            poleArm.transform.rotation = holdRotation;
            elapsedTime = 0f;
            currentTargetHandle = poleArm.tracker.AddTarget(poleArm.hand, followPosition: true, followRotation: false);

            if (holdDuration <= 0f) {
                poleArm.ChangeState(new EmittionState(poleArm, holdRotation, speed, speedDuration, speedCurve));
            }
        }

        public void OnExit() {
            poleArm.tracker.RemoveTarget(currentTargetHandle);
            currentTargetHandle = null;
        }

        public void OnUpdate(float deltaTime) {
            elapsedTime += deltaTime;
            if (elapsedTime >= holdDuration) {
                poleArm.ChangeState(new EmittionState(poleArm, holdRotation, speed, speedDuration, speedCurve));
            }
        }

        public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, float speedDuration, AnimationCurve speedCurve, Vector3 postAimHoldAdjustmentEulerAngles) {
        }

        public void EndEmit() {
            poleArm.ChangeState(new DefaultState(poleArm));
        }
    }

    class EmittionState : IState {
        readonly PoleArm poleArm;
        readonly Quaternion finalRotation;
        readonly float speed;
        readonly float speedDuration;
        readonly AnimationCurve speedCurve;
        float elapsedTime;
        Vector3 emitDirection;
        Tracker.TargetHandle pauseTargetHandle;
        bool hasHitHurtbox;
        bool isStopped;

        public EmittionState(PoleArm poleArm, Quaternion finalRotation, float speed, float speedDuration, AnimationCurve speedCurve) {
            this.poleArm = poleArm;
            this.finalRotation = finalRotation;
            this.speed = speed;
            this.speedDuration = Mathf.Max(speedDuration, 0.0001f);
            this.speedCurve = speedCurve;
        }

        public void OnEnter() {
            elapsedTime = 0f;
            hasHitHurtbox = false;
            isStopped = false;
            pauseTargetHandle = poleArm.tracker.AddTarget();
            poleArm.transform.rotation = finalRotation;
            poleArm.transform.SetParent(null);
            emitDirection = poleArm.transform.forward;

            Vector3 centerPos = poleArm.characterCenter != null ? poleArm.characterCenter.position : poleArm.ownerRoot.position + Vector3.up * 1f;
            Vector3 weaponPos = poleArm.transform.position;
            Vector3 dir = weaponPos - centerPos;
            float dist = dir.magnitude;

            Debug.Log($"[PoleArm OnEnter] center={centerPos}, weapon={weaponPos}, dist={dist}");

            if (dist > 0.001f) {
                // 1. まず通常のRaycastで壁を検知
                var hits = Physics.RaycastAll(centerPos, dir.normalized, dist, poleArm.hitMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                bool hitWall = false;
                Debug.Log($"[PoleArm OnEnter] RaycastAll hits={hits.Length}");

                foreach (var hit in hits) {
                    Debug.Log($"[PoleArm OnEnter] Ray hit: {hit.collider.name}, root: {hit.collider.transform.root.name}");
                    if (hit.collider.transform.root == poleArm.ownerRoot || hit.collider.transform.root == poleArm.transform.root) continue;

                    var hurtbox = hit.collider.GetComponentInParent<Hurtbox>();
                    bool isWall = hurtbox == null || ((poleArm.wallMask & (1 << hit.collider.gameObject.layer)) != 0);

                    if (hurtbox != null && !hasHitHurtbox) {
                        hasHitHurtbox = true;
                        poleArm.onHitHurtbox.OnNext(hurtbox);
                        poleArm.PlayHitEffect(hit.point, dir);
                    }

                    if (isWall) {
                        Debug.Log($"[PoleArm OnEnter] Ray hit WALL: {hit.collider.name}");
                        var impactPos = centerPos + dir.normalized * hit.distance;
                        StickToWall(impactPos, hit.normal);
                        hitWall = true;
                        break;
                    }
                }

                // 2. 超近距離でのめり込み対策
                if (!hitWall && !isStopped) {
                    var overlapsLine = Physics.OverlapCapsule(centerPos, weaponPos, 0.05f, poleArm.hitMask, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(overlapsLine, (a, b) => Vector3.SqrMagnitude(a.ClosestPoint(centerPos) - centerPos).CompareTo(Vector3.SqrMagnitude(b.ClosestPoint(centerPos) - centerPos)));
                    Debug.Log($"[PoleArm OnEnter] OverlapCapsule hits={overlapsLine.Length}");
                    foreach (var col in overlapsLine) {
                        Debug.Log($"[PoleArm OnEnter] Capsule hit: {col.name}, root: {col.transform.root.name}");
                        if (col.transform.root == poleArm.ownerRoot || col.transform.root == poleArm.transform.root) continue;
                        var hurtbox = col.GetComponentInParent<Hurtbox>();
                        bool isWall = hurtbox == null || ((poleArm.wallMask & (1 << col.gameObject.layer)) != 0);

                        if (hurtbox != null && !hasHitHurtbox) {
                            hasHitHurtbox = true;
                            poleArm.onHitHurtbox.OnNext(hurtbox);
                            poleArm.PlayHitEffect(col.ClosestPoint(poleArm.transform.position), emitDirection);
                        }

                        if (isWall) {
                            Debug.Log($"[PoleArm OnEnter] Capsule hit WALL: {col.name}");
                            StickToWall(poleArm.transform.position, emitDirection * -1f);
                            return;
                        }
                    }
                }
            }

            if (isStopped) return;

            // 3. 武器の出現位置自体が壁の中に埋まっていないかチェック
            var overlaps = Physics.OverlapSphere(poleArm.transform.position, poleArm.hitRadius, poleArm.hitMask, QueryTriggerInteraction.Ignore);
            var sphereCenter = poleArm.transform.position;
            System.Array.Sort(overlaps, (a, b) => Vector3.SqrMagnitude(a.ClosestPoint(sphereCenter) - sphereCenter).CompareTo(Vector3.SqrMagnitude(b.ClosestPoint(sphereCenter) - sphereCenter)));
            Debug.Log($"[PoleArm OnEnter] OverlapSphere hits={overlaps.Length}");
            foreach (var col in overlaps) {
                Debug.Log($"[PoleArm OnEnter] Sphere hit: {col.name}, root: {col.transform.root.name}");
                if (col.transform.root == poleArm.ownerRoot || col.transform.root == poleArm.transform.root) continue;
                
                var hurtbox = col.GetComponentInParent<Hurtbox>();
                bool isWall = hurtbox == null || ((poleArm.wallMask & (1 << col.gameObject.layer)) != 0);

                if (hurtbox != null && !hasHitHurtbox) {
                    hasHitHurtbox = true;
                    poleArm.onHitHurtbox.OnNext(hurtbox);
                    poleArm.PlayHitEffect(col.ClosestPoint(poleArm.transform.position), emitDirection);
                }

                if (isWall) {
                    Debug.Log($"[PoleArm OnEnter] Sphere hit WALL: {col.name}");
                    StickToWall(poleArm.transform.position, emitDirection * -1f);
                    return;
                }
            }

            poleArm.emitEffect.Play();
        }

        public void OnExit() {
            if (pauseTargetHandle != null) {
                poleArm.tracker.RemoveTarget(pauseTargetHandle);
                pauseTargetHandle = null;
            }
        }

        public void OnUpdate(float deltaTime) {
            if (isStopped) return;

            elapsedTime += deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / speedDuration);
            float moveSpeed = speed * speedCurve.Evaluate(normalizedTime);
            float distanceToMove = moveSpeed * deltaTime;
            var hits = Physics.SphereCastAll(poleArm.transform.position, poleArm.hitRadius, emitDirection, distanceToMove, poleArm.hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool wallHit = false;
            foreach (var hit in hits) {
                if (hit.collider.transform.root == poleArm.ownerRoot || hit.collider.transform.root == poleArm.transform.root) continue;

                var hurtbox = hit.collider.GetComponentInParent<Hurtbox>();
                bool isWall = hurtbox == null || ((poleArm.wallMask & (1 << hit.collider.gameObject.layer)) != 0);

                if (hurtbox != null && !hasHitHurtbox) {
                    hasHitHurtbox = true;
                    poleArm.onHitHurtbox.OnNext(hurtbox);
                    poleArm.PlayHitEffect(hit.point, emitDirection);
                }
                
                if (isWall) {
                    var impactPos = poleArm.transform.position + emitDirection * hit.distance;
                    StickToWall(impactPos, hit.normal);
                    wallHit = true;
                    break;
                }
            }

            if (!wallHit) {
                poleArm.transform.position += emitDirection * distanceToMove;
            }
        }

        public void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, float speedDuration, AnimationCurve speedCurve, Vector3 postAimHoldAdjustmentEulerAngles) {
        }

        public void EndEmit() {
            poleArm.ChangeState(new DefaultState(poleArm));
        }

        void StickToWall(Vector3 impactPos, Vector3 wallNormal) {
            var stickDirection = emitDirection.sqrMagnitude > 0.000001f ? emitDirection.normalized : (-wallNormal).normalized;
            poleArm.transform.position = impactPos + stickDirection * poleArm.wallStickDepth;
            poleArm.onHitWall.OnNext((poleArm.transform.position, wallNormal));
            isStopped = true;
        }
    }

    private interface IState {
        void OnEnter();
        void OnExit();
        void OnUpdate(float deltaTime);
        void BeginEmit(Quaternion finalRotation, float holdDuration, float speed, float speedDuration, AnimationCurve speedCurve, Vector3 postAimHoldAdjustmentEulerAngles);
        void EndEmit();
    }
}
