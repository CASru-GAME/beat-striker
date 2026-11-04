using UnityEngine;
using System.Collections;
using System;
using System.Threading.Tasks;
using R3;
using PlayerId = App.PlayerId;

namespace Alice {
    [RequireComponent(typeof(Camera))]
    public class StageCamera : MonoBehaviour {
        [Header("Transforms")]
        [SerializeField] private Transform camTransform0;

        [Header("Settings")]
        [SerializeField] private float forwardDistance = 2f;
        [SerializeField] private float forwardDuration = 1f;
        [SerializeField] private float orbitDuration = 3f;
        [SerializeField] private float orbitAngle = -20f;
        [SerializeField] private float outroDistance = 3f;
        [SerializeField] private float outroDuration = 1f;
        [SerializeField] private float outroWaitDuration = 3f;
        [SerializeField] private float outroPostWaitDuration = 0f;
        [SerializeField] private float maxPlayersDistanceToFitOnScreen = 12f;
        [SerializeField] private float minCameraDepthForFitPlayers = 0f;
        [SerializeField] private AnimationCurve normalizedDistanceToDiagonalRatio = new AnimationCurve(
            new Keyframe(0f, 0.28f),
            new Keyframe(1f, 0.4f));
        [SerializeField] private float zoomInSmoothTime = 0.45f;
        [SerializeField] private float zoomOutSmoothTime = 0.3f;
        [SerializeField] private float centerSmoothTime = 0.25f;
        [SerializeField, Min(0f)] private float airborneVerticalMargin = .75f;
        [SerializeField, Min(.01f)] private float verticalCenterSmoothTime = .6f;
        [SerializeField, Min(0f)] private float attentionFramingHoldSeconds = 1.3f;
        [SerializeField] private Vector2 centerViewportOffset = Vector2.zero;
        [SerializeField] private float shakeDamping = 18f;
        [SerializeField] private float viewBeatPulseZoomDistance = 0.16f;
        [SerializeField] private float viewBeatPulseExpandDuration = 0.05f;
        [SerializeField] private float viewBeatPulseReturnDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float attentionZoomDistance = 3.2f;
        [SerializeField, Min(0.01f)] private float attentionZoomSmoothTime = 0.23333333f;
        [SerializeField, Min(0.01f)] private float attentionReturnSmoothTime = 0.17777778f;
        [SerializeField, Min(0.01f)] private float attentionMoveMaxSpeed = 18f;
        [SerializeField, Min(0.01f)] private float attentionReturnMaxSpeed = 27f;
        [SerializeField] private float attentionFocusPositionThreshold = 0.03f;
        [SerializeField] private float attentionFocusRotationThresholdDeg = 1.2f;
        [SerializeField] private UnityEngine.Rendering.Volume volume;
        [SerializeField] private bool enableVolumeOnViewBeatPulse = true;

        private Camera stageCamera;
        private float cameraDepthVelocity;
        private Vector3 centerMoveVelocity;
        Vector3 shakeOffset;
        Vector3 shakeVelocity;
        Vector3 previousShakeOffset;
        Vector3 viewBeatPulseOffset;
        Vector3 previousViewBeatPulseOffset;
        float viewBeatPulseElapsedSeconds = -1f;
        private Vector2 desiredCenterViewport;
        float attentionFramingMinimumDepth, attentionFramingExpires;
        CameraState currentState;
        Func<int, Vector3> playerCenterPositionResolver;
        Action<int> introPoseRequester;
        Action<int> victoryPoseRequester;
        Vector3 initialCameraPosition;
        float initialSideSign;
        bool hasInitialSideSign;
        Vector3 firstRoundStartPosition;
        Quaternion firstRoundStartRotation;
        bool hasFirstRoundStartPose;
        IdleCameraState idleState;
        BattleCameraState battleState;
        AttentionCameraState attentionState;
        AttentionReturnCameraState attentionReturnState;
        SequenceCameraState sequenceState;
        readonly AttentionRuntime attentionRuntime = new();
        readonly Subject<bool> attentionFocusStateChangedSubject = new();
        readonly Subject<bool> attentionActiveStateChangedSubject = new();

        public Observable<bool> OnAttentionFocusStateChanged => attentionFocusStateChangedSubject;
        public Observable<bool> OnAttentionActiveStateChanged => attentionActiveStateChangedSubject;
        public bool IsAttentionPresentationActive => currentState != null &&
            (currentState == attentionState || currentState == attentionReturnState);

        enum CameraSequencePhase {
            None,
            IntroForward,
            IntroPlayer0,
            IntroPlayer1,
            OutroPreWait,
            OutroMove,
            OutroPostWait,
        }

        sealed class AttentionRuntime {
            public int TargetPlayerId;
            public float RemainingSeconds;
            public float ZoomSmoothTime;
            public Quaternion BaseRotation;
            public Vector3 BaseForward;
            public Vector3 MoveVelocity;
            public bool IsFocused;
            public bool IsActive;
            public Action ReturnCompleted;
            public Action ReturnStarted;
            public float ZoomDistance;
            public bool ResumeOnReturnStarted;

            public void Reset() {
                RemainingSeconds = 0f;
                MoveVelocity = Vector3.zero;
                IsFocused = false;
                IsActive = false;
                ReturnCompleted = null;
                ReturnStarted = null;
                ResumeOnReturnStarted = false;
            }
        }

        abstract class CameraState {
            protected readonly StageCamera owner;

            protected CameraState(StageCamera owner) {
                this.owner = owner;
            }

            public virtual void OnEnter() { }
            public virtual void OnExit() { }
            public virtual void OnUpdate() { }
            public virtual void OnAttentionRequested(int playerId, float durationSeconds) { }
            public virtual void OnSequenceSkipRequested() { }
            public virtual bool AllowsViewBeatPulse => false;
        }

        sealed class IdleCameraState : CameraState {
            public IdleCameraState(StageCamera owner) : base(owner) { }

            public override void OnEnter() {
                owner.SetAttentionActiveState(false);
                owner.SetAttentionFocusState(false);
                owner.attentionRuntime.Reset();
            }
        }

        sealed class BattleCameraState : CameraState {
            public BattleCameraState(StageCamera owner) : base(owner) { }

            public override bool AllowsViewBeatPulse => true;

            public override void OnEnter() {
                owner.SetAttentionActiveState(false);
                owner.attentionRuntime.Reset();
                owner.cameraDepthVelocity = 0f;
                owner.centerMoveVelocity = Vector3.zero;
            }

            public override void OnUpdate() {
                owner.MoveCameraForwardToFitPlayers();
                owner.MoveParallelToPlayersCenter();
            }

            public override void OnAttentionRequested(int playerId, float durationSeconds) {
                if (durationSeconds <= 0f) {
                    return;
                }

                owner.attentionRuntime.BaseRotation = owner.transform.rotation;
                owner.attentionRuntime.BaseForward = owner.transform.forward;
                owner.attentionRuntime.TargetPlayerId = playerId;
                owner.attentionRuntime.RemainingSeconds = durationSeconds;
                owner.attentionRuntime.MoveVelocity = owner.centerMoveVelocity - owner.transform.forward * owner.cameraDepthVelocity;
                owner.SetCameraState(owner.attentionState);
            }
        }

        sealed class AttentionCameraState : CameraState {
            public AttentionCameraState(StageCamera owner) : base(owner) { }

            public override void OnEnter() {
                owner.SetAttentionActiveState(true);
                owner.SetAttentionFocusState(false);
            }

            public override void OnExit() {
                owner.SetAttentionFocusState(false);
            }

            public override void OnUpdate() {
                owner.attentionRuntime.RemainingSeconds -= Time.deltaTime;
                var targetCenter = owner.GetPlayerCenterPosition(owner.attentionRuntime.TargetPlayerId);
                var targetPosition = targetCenter - owner.attentionRuntime.BaseForward * owner.attentionRuntime.ZoomDistance;
                owner.transform.position = Vector3.SmoothDamp(
                    owner.transform.position,
                    targetPosition,
                    ref owner.attentionRuntime.MoveVelocity,
                    owner.attentionRuntime.ZoomSmoothTime,
                    owner.attentionMoveMaxSpeed);
                // Keep the battle viewing direction throughout the close-up. Turning
                // toward the striker before the dolly settles swings the background.
                owner.transform.rotation = owner.attentionRuntime.BaseRotation;

                if (!owner.attentionRuntime.IsFocused) {
                    var settled = owner.IsAttentionFocusSettled(targetCenter, targetPosition);
                    if (settled) {
                        owner.SetAttentionFocusState(true);
                    }
                }

                if (owner.attentionRuntime.RemainingSeconds > 0f) {
                    return;
                }

                owner.attentionRuntime.RemainingSeconds = 0f;
                owner.SetCameraState(owner.attentionReturnState);
            }

            public override void OnAttentionRequested(int playerId, float durationSeconds) {
                if (durationSeconds <= 0f) {
                    return;
                }

                owner.attentionRuntime.TargetPlayerId = playerId;
                owner.attentionRuntime.RemainingSeconds = durationSeconds;
                owner.SetAttentionFocusState(false);
            }
        }

        sealed class AttentionReturnCameraState : CameraState {
            public AttentionReturnCameraState(StageCamera owner) : base(owner) { }

            public override void OnEnter() {
                owner.SetAttentionActiveState(!owner.attentionRuntime.ResumeOnReturnStarted);
                var returnStarted = owner.attentionRuntime.ReturnStarted;
                owner.attentionRuntime.ReturnStarted = null;
                returnStarted?.Invoke();
            }

            public override void OnUpdate() {
                // Return straight to the live battle framing instead of visiting the
                // pre-special position and then correcting again in BattleCameraState.
                var targetPosition = owner.GetBattleFramingPosition(owner.attentionRuntime.BaseRotation);
                owner.transform.position = Vector3.SmoothDamp(
                    owner.transform.position,
                    targetPosition,
                    ref owner.attentionRuntime.MoveVelocity,
                    owner.attentionReturnSmoothTime,
                    owner.attentionReturnMaxSpeed);
                owner.transform.rotation = owner.attentionRuntime.BaseRotation;

                bool reachedPosition = (owner.transform.position - targetPosition).sqrMagnitude <= 0.0004f;
                if (!reachedPosition) {
                    return;
                }

                var moveVelocity = owner.attentionRuntime.MoveVelocity;
                var returnCompleted = owner.attentionRuntime.ReturnCompleted;
                owner.SetCameraState(owner.battleState);
                owner.cameraDepthVelocity = -Vector3.Dot(moveVelocity, owner.transform.forward);
                owner.centerMoveVelocity = Vector3.ProjectOnPlane(moveVelocity, owner.transform.forward);
                returnCompleted?.Invoke();
            }

            public override void OnAttentionRequested(int playerId, float durationSeconds) {
                if (durationSeconds <= 0f) {
                    return;
                }

                owner.attentionRuntime.TargetPlayerId = playerId;
                owner.attentionRuntime.RemainingSeconds = durationSeconds;
                owner.SetCameraState(owner.attentionState);
            }
        }

        sealed class SequenceCameraState : CameraState {
            bool isSkipRequested;
            CameraSequencePhase currentSequencePhase;
            TaskCompletionSource<bool> introCompletionSource;
            TaskCompletionSource<bool> outroCompletionSource;

            public SequenceCameraState(StageCamera owner) : base(owner) { }

            public override void OnEnter() {
                owner.SetAttentionActiveState(false);
                owner.SetAttentionFocusState(false);
                owner.attentionRuntime.Reset();
                isSkipRequested = false;
                currentSequencePhase = CameraSequencePhase.None;
            }

            public Task PlayIntroAsync() {
                introCompletionSource?.TrySetCanceled();
                introCompletionSource = new TaskCompletionSource<bool>();
                owner.StopAllCoroutines();
                owner.StartCoroutine(StartCameraSequence());
                return introCompletionSource.Task;
            }

            public Task PlayOutroAsync(PlayerId winner) {
                outroCompletionSource?.TrySetCanceled();
                outroCompletionSource = new TaskCompletionSource<bool>();
                owner.StopAllCoroutines();
                owner.StartCoroutine(MoveToWinner(winner.Value));
                return outroCompletionSource.Task;
            }

            public override void OnSequenceSkipRequested() {
                isSkipRequested = true;
            }

            public void SetPhase(CameraSequencePhase phase) {
                currentSequencePhase = phase;
            }

            public bool ConsumeSkipIfRequested(CameraSequencePhase expectedPhase) {
                if (!isSkipRequested || currentSequencePhase != expectedPhase) {
                    return false;
                }

                isSkipRequested = false;
                return true;
            }

            public IEnumerator WaitForSecondsSkippable(float duration, CameraSequencePhase phase) {
                currentSequencePhase = phase;
                float elapsedTime = 0f;
                while (elapsedTime < duration) {
                    if (ConsumeSkipIfRequested(phase)) {
                        yield break;
                    }

                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
            }

            IEnumerator StartCameraSequence() {
                Vector3 startPosition = owner.transform.position;
                Vector3 forwardPosition = startPosition + owner.transform.forward * owner.forwardDistance;

                SetPhase(CameraSequencePhase.IntroForward);
                float elapsedTime = 0f;
                while (elapsedTime < owner.forwardDuration) {
                    if (ConsumeSkipIfRequested(CameraSequencePhase.IntroForward)) {
                        break;
                    }

                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / owner.forwardDuration;
                    owner.transform.position = Vector3.Lerp(startPosition, forwardPosition, t);
                    yield return null;
                }
                owner.transform.position = forwardPosition;

                SetPhase(CameraSequencePhase.IntroPlayer0);
                owner.transform.position = owner.camTransform0.position;
                owner.introPoseRequester?.Invoke(0);
                owner.LookAt(owner.GetPlayerCenterPosition(0));
                yield return OrbitAroundTarget(0, owner.orbitDuration, owner.orbitAngle, CameraSequencePhase.IntroPlayer0);

                SetPhase(CameraSequencePhase.IntroPlayer1);
                owner.transform.position = owner.GetInferredCamPosition1();
                owner.introPoseRequester?.Invoke(1);
                owner.LookAt(owner.GetPlayerCenterPosition(1));
                yield return OrbitAroundTarget(1, owner.orbitDuration, -owner.orbitAngle, CameraSequencePhase.IntroPlayer1);

                owner.InitializeBattleStartCameraPose();
                owner.SetCameraState(owner.idleState);
                SetPhase(CameraSequencePhase.None);
                introCompletionSource?.TrySetResult(true);
            }

            IEnumerator OrbitAroundTarget(int playerId, float duration, float angle, CameraSequencePhase phase) {
                Vector3 pivot = owner.GetPlayerCenterPosition(playerId);
                Vector3 startOffset = owner.transform.position - pivot;
                float elapsedTime = 0f;

                while (elapsedTime < duration) {
                    if (ConsumeSkipIfRequested(phase)) {
                        break;
                    }

                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / duration;
                    float currentAngle = Mathf.Lerp(0f, angle, t);

                    pivot = owner.GetPlayerCenterPosition(playerId);
                    Quaternion rotation = Quaternion.Euler(0, currentAngle, 0);
                    Vector3 newPos = pivot + rotation * startOffset;

                    owner.transform.position = newPos;
                    owner.LookAt(pivot);

                    yield return null;
                }
            }

            IEnumerator MoveToWinner(int winnerPlayerId) {
                owner.victoryPoseRequester?.Invoke(winnerPlayerId);

                Vector3 startPosition = owner.transform.position;
                Quaternion startRotation = owner.transform.rotation;
                Vector3 winnerCenterPosition = owner.GetPlayerCenterPosition(winnerPlayerId);
                Vector3 direction = (winnerCenterPosition - owner.transform.position).normalized;
                Vector3 targetPosition = winnerCenterPosition - direction * owner.outroDistance;
                Quaternion targetRotation = Quaternion.LookRotation(winnerCenterPosition - targetPosition);

                SetPhase(CameraSequencePhase.OutroMove);
                float elapsedTime = 0f;

                while (elapsedTime < owner.outroDuration) {
                    if (ConsumeSkipIfRequested(CameraSequencePhase.OutroMove)) {
                        break;
                    }

                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / owner.outroDuration;
                    t = t * t * (3f - 2f * t);
                    owner.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                    owner.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                    yield return null;
                }

                owner.transform.position = targetPosition;
                owner.LookAt(owner.GetPlayerCenterPosition(winnerPlayerId));

                yield return WaitForSecondsSkippable(owner.outroPostWaitDuration, CameraSequencePhase.OutroPostWait);
                SetPhase(CameraSequencePhase.None);
                owner.SetCameraState(owner.idleState);
                outroCompletionSource?.TrySetResult(true);
            }
        }

        void Awake() {
            EnsureRuntimeInitialized();
        }

        public void SetPlayerCenterPositionResolver(Func<int, Vector3> resolver) {
            playerCenterPositionResolver = resolver;
        }

        public void SetIntroPoseRequester(Action<int> requester) {
            introPoseRequester = requester;
        }

        public void SetVictoryPoseRequester(Action<int> requester) {
            victoryPoseRequester = requester;
        }

        void Start() {
            EnsureRuntimeInitialized();
        }

        void OnDestroy() {
            attentionFocusStateChangedSubject.Dispose();
            attentionActiveStateChangedSubject.Dispose();
        }

        void LateUpdate() {
            EnsureRuntimeInitialized();
            // Presentation offsets must not feed back into tracking or its velocity.
            transform.position -= previousViewBeatPulseOffset + previousShakeOffset;
            previousViewBeatPulseOffset = Vector3.zero;
            previousShakeOffset = Vector3.zero;
            currentState?.OnUpdate();
            ApplyViewBeatPulseOffset();
            ApplyShakeOffset();
        }

        public Task PresentIntroAsync() {
            EnsureRuntimeInitialized();
            SetCameraState(sequenceState);
            return sequenceState.PlayIntroAsync();
        }

        private void LookAt(Vector3 targetPosition) {
            transform.LookAt(targetPosition);
        }

        public void PresentRoundPlayableStart() {
            EnsureRuntimeInitialized();
            SetCameraState(battleState);
        }

        public void ResetRoundCamera() {
            EnsureRuntimeInitialized();
            attentionFramingMinimumDepth = attentionFramingExpires = 0f;
            StopAllCoroutines();
            SetCameraState(idleState);
            InitializeBattleStartCameraPose();
            cameraDepthVelocity = 0f;
            centerMoveVelocity = Vector3.zero;
            shakeOffset = Vector3.zero;
            shakeVelocity = Vector3.zero;
            previousShakeOffset = Vector3.zero;
        }

        public void PresentRoundFinish() {
            EnsureRuntimeInitialized();
            SetCameraState(idleState);
        }

        public void PresentBattleFinish() {
            EnsureRuntimeInitialized();
            SetCameraState(idleState);
        }

        public Task PresentOutroAsync(PlayerId winner) {
            EnsureRuntimeInitialized();
            SetCameraState(sequenceState);
            return sequenceState.PlayOutroAsync(winner);
        }

        public void RequestSequenceSkip() {
            EnsureRuntimeInitialized();
            currentState?.OnSequenceSkipRequested();
        }

        public void RequestShake(StrikerImpact command) {
            shakeVelocity += command.DirectionAndMagnitude;
        }

        public void RequestViewBeatPulse() {
            EnsureRuntimeInitialized();

            if (currentState == null || !currentState.AllowsViewBeatPulse) {
                return;
            }

            viewBeatPulseElapsedSeconds = 0f;
        }

        public void RequestAttention(int playerId, float durationSeconds, Action onReturnCompleted = null, float? zoomSmoothTime = null, Action onReturnStarted = null, float? zoomDistance = null, bool resumeOnReturnStarted = false) {
            EnsureRuntimeInitialized();
            if (durationSeconds <= 0f) {
                return;
            }

            currentState?.OnAttentionRequested(playerId, durationSeconds);
            if (currentState == attentionState || currentState == attentionReturnState) {
                attentionRuntime.ZoomSmoothTime = Mathf.Max(attentionZoomSmoothTime, zoomSmoothTime ?? attentionZoomSmoothTime);
                attentionRuntime.ReturnCompleted += onReturnCompleted;
                attentionRuntime.ReturnStarted += onReturnStarted;
                attentionRuntime.ZoomDistance = Mathf.Max(.01f, zoomDistance ?? attentionZoomDistance);
                attentionRuntime.ResumeOnReturnStarted = resumeOnReturnStarted;
                attentionFramingMinimumDepth = zoomDistance ?? 0f;
                attentionFramingExpires = Time.time + durationSeconds + attentionFramingHoldSeconds;
            }
        }

        void SetAttentionFocusState(bool isFocused) {
            if (attentionRuntime.IsFocused == isFocused) {
                return;
            }

            attentionRuntime.IsFocused = isFocused;
            attentionFocusStateChangedSubject.OnNext(isFocused);
        }

        void SetAttentionActiveState(bool isActive) {
            if (attentionRuntime.IsActive == isActive) {
                return;
            }

            attentionRuntime.IsActive = isActive;
            attentionActiveStateChangedSubject.OnNext(isActive);
        }

        bool IsAttentionFocusSettled(Vector3 targetCenter, Vector3 targetPosition) {
            var positionDelta = (transform.position - targetPosition).sqrMagnitude;
            if (positionDelta > attentionFocusPositionThreshold * attentionFocusPositionThreshold) {
                return false;
            }

            var lookDirection = targetCenter - transform.position;
            if (lookDirection.sqrMagnitude <= 0.000001f) {
                return true;
            }

            var targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            return Quaternion.Angle(transform.rotation, targetRotation) <= attentionFocusRotationThresholdDeg;
        }

        void EnsureRuntimeInitialized() {
            if (stageCamera == null) {
                stageCamera = GetComponent<Camera>();
            }

            if (idleState != null) {
                return;
            }

            initialCameraPosition = transform.position;
            idleState = new IdleCameraState(this);
            battleState = new BattleCameraState(this);
            attentionState = new AttentionCameraState(this);
            attentionReturnState = new AttentionReturnCameraState(this);
            sequenceState = new SequenceCameraState(this);
            SetCameraState(idleState);
        }

        void ResetViewBeatPulse() {
            transform.position -= previousViewBeatPulseOffset;
            viewBeatPulseElapsedSeconds = -1f;
            viewBeatPulseOffset = Vector3.zero;
            previousViewBeatPulseOffset = Vector3.zero;
            if (enableVolumeOnViewBeatPulse) {
                volume.weight = 0f;
            }
        }

        void ApplyViewBeatPulseOffset() {
            if (viewBeatPulseElapsedSeconds < 0f) {
                previousViewBeatPulseOffset = Vector3.zero;
                viewBeatPulseOffset = Vector3.zero;
                return;
            }

            viewBeatPulseElapsedSeconds += Time.deltaTime;

            var expandDuration = Mathf.Max(0.0001f, viewBeatPulseExpandDuration);
            var returnDuration = Mathf.Max(0.0001f, viewBeatPulseReturnDuration);
            var totalDuration = expandDuration + returnDuration;

            if (viewBeatPulseElapsedSeconds >= totalDuration) {
                viewBeatPulseElapsedSeconds = -1f;
                viewBeatPulseOffset = Vector3.zero;
                previousViewBeatPulseOffset = Vector3.zero;
                return;
            }

            float pulseScale;
            if (viewBeatPulseElapsedSeconds <= expandDuration) {
                var t = viewBeatPulseElapsedSeconds / expandDuration;
                pulseScale = Mathf.Lerp(1f, 1f + viewBeatPulseZoomDistance, t);
                if (enableVolumeOnViewBeatPulse) {
                    volume.weight = Mathf.Lerp(0f, 1f, t);
                }
            }
            else {
                var t = (viewBeatPulseElapsedSeconds - expandDuration) / returnDuration;
                pulseScale = Mathf.Lerp(1f + viewBeatPulseZoomDistance, 1f, t);
                if (enableVolumeOnViewBeatPulse) {
                    volume.weight = Mathf.Lerp(1f, 0f, t);
                }
            }

            viewBeatPulseOffset = transform.forward * (pulseScale - 1f);
            previousViewBeatPulseOffset = viewBeatPulseOffset;
            transform.position += previousViewBeatPulseOffset;
        }

        void ApplyShakeOffset() {
            if (shakeVelocity.sqrMagnitude <= 0.000001f && shakeOffset.sqrMagnitude <= 0.000001f) {
                shakeVelocity = Vector3.zero;
                shakeOffset = Vector3.zero;
                previousShakeOffset = Vector3.zero;
                return;
            }

            shakeOffset += shakeVelocity * Time.deltaTime;
            float damping = 1f - Mathf.Exp(-shakeDamping * Time.deltaTime);
            shakeVelocity = Vector3.Lerp(shakeVelocity, Vector3.zero, damping);
            shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, damping);

            previousShakeOffset = shakeOffset;
            transform.position += previousShakeOffset;
        }

        void SetCameraState(CameraState nextState) {
            if (nextState == null || ReferenceEquals(currentState, nextState)) {
                return;
            }

            currentState?.OnExit();
            currentState = nextState;
            currentState.OnEnter();

            if (!currentState.AllowsViewBeatPulse) {
                ResetViewBeatPulse();
            }
        }

        private Vector3 GetPlayersCenter() {
            return (GetPlayerCenterPosition(0) + GetPlayerCenterPosition(1)) * 0.5f;
        }

        private void MoveParallelToPlayersCenter() {
            Vector3 center = GetPlayersCenter();
            Vector3 centerInCameraSpace = transform.InverseTransformPoint(center);
            float depth = Mathf.Max(0.01f, centerInCameraSpace.z);
            float halfVerticalFovRad = stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalfVertical = Mathf.Tan(halfVerticalFovRad);
            float tanHalfHorizontal = tanHalfVertical * stageCamera.aspect;

            float desiredLocalX = (desiredCenterViewport.x - 0.5f) * 2f * depth * tanHalfHorizontal;
            float desiredLocalY = (desiredCenterViewport.y - 0.5f) * 2f * depth * tanHalfVertical;

            Vector3 moveInCameraSpace = new Vector3(
                centerInCameraSpace.x - desiredLocalX,
                centerInCameraSpace.y - desiredLocalY,
                0f);
            Vector3 localVelocity = transform.InverseTransformVector(centerMoveVelocity);
            float moveX = Mathf.SmoothDamp(0f, moveInCameraSpace.x, ref localVelocity.x, centerSmoothTime);
            float moveY = Mathf.SmoothDamp(0f, moveInCameraSpace.y, ref localVelocity.y, verticalCenterSmoothTime);
            centerMoveVelocity = transform.TransformVector(localVelocity);
            transform.position += transform.TransformVector(new Vector3(moveX, moveY, 0f));
        }

        private void MoveCameraForwardToFitPlayers() {
            Vector3 camSpace0 = transform.InverseTransformPoint(GetPlayerCenterPosition(0));
            Vector3 camSpace1 = transform.InverseTransformPoint(GetPlayerCenterPosition(1));

            float currentDepth = Mathf.Max(0.01f, (camSpace0.z + camSpace1.z) * 0.5f);
            float tanHalfVertical = Mathf.Max(0.0001f, Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float targetDepth = CalculateTargetDepthToFitPlayers(camSpace0, camSpace1, tanHalfVertical);
            float zoomSmoothTime = targetDepth < currentDepth ? zoomInSmoothTime : zoomOutSmoothTime;

            float smoothedDepth = Mathf.SmoothDamp(
                currentDepth,
                targetDepth,
                ref cameraDepthVelocity,
                Mathf.Max(0.0001f, zoomSmoothTime));
            float depthDelta = currentDepth - smoothedDepth;

            transform.position += transform.forward * depthDelta;
        }

        private float CalculateDiagonalDistanceRatio(float playersDistance) {
            float safeMaxDistance = Mathf.Max(0.01f, maxPlayersDistanceToFitOnScreen);
            float normalizedDistance = Mathf.Clamp01(playersDistance / safeMaxDistance);
            float diagonalDistanceRatio = normalizedDistanceToDiagonalRatio.Evaluate(normalizedDistance);
            return Mathf.Clamp(diagonalDistanceRatio, 0.05f, 0.95f);
        }

        private float CalculateTargetDepthToFitPlayers(Vector3 camSpace0, Vector3 camSpace1, float tanHalfVertical) {
            Vector2 playersDeltaInCameraPlane = new Vector2(
                camSpace1.x - camSpace0.x,
                camSpace1.y - camSpace0.y);
            float playersDistance = playersDeltaInCameraPlane.magnitude;
            float diagonalDistanceRatio = CalculateDiagonalDistanceRatio(playersDistance);
            float halfHorizontalSpan = Mathf.Abs(playersDeltaInCameraPlane.x) * 0.5f;
            float halfVerticalSpan = Mathf.Abs(playersDeltaInCameraPlane.y) * 0.5f;
            float horizontalDepth = halfHorizontalSpan / (tanHalfVertical * stageCamera.aspect * diagonalDistanceRatio);
            float aerialMargin = airborneVerticalMargin * Mathf.Clamp01(halfVerticalSpan);
            // Keep both players inside the frame even when the center is shifted down.
            float verticalRatio = Mathf.Min(diagonalDistanceRatio,
                Mathf.Max(0.05f, 0.9f - 2f * Mathf.Abs(desiredCenterViewport.y - 0.5f)));
            float verticalDepth = (halfVerticalSpan + aerialMargin) / (tanHalfVertical * verticalRatio);
            float attentionMinimum = Time.time < attentionFramingExpires ? attentionFramingMinimumDepth : 0f;
            return Mathf.Max(horizontalDepth, verticalDepth, minCameraDepthForFitPlayers, attentionMinimum);
        }

        private Vector3 GetPlayersLineOnPlane() {
            Vector3 line = GetPlayerCenterPosition(1) - GetPlayerCenterPosition(0);
            line.y = 0f;
            if (line.sqrMagnitude < 0.0001f) {
                return Vector3.right;
            }

            return line.normalized;
        }

        private void InitializeBattleStartCameraPose() {
            if (hasFirstRoundStartPose) {
                transform.SetPositionAndRotation(firstRoundStartPosition, firstRoundStartRotation);
                desiredCenterViewport = GetDesiredCenterViewport();
                return;
            }

            Vector3 center = GetPlayersCenter();
            Vector3 playerLine = GetPlayersLineOnPlane();
            Vector3 sideAxis = Vector3.Cross(Vector3.up, playerLine).normalized;

            if (!hasInitialSideSign) {
                Vector3 initialOffset = initialCameraPosition - center;
                float sideDot = Vector3.Dot(Vector3.ProjectOnPlane(initialOffset, Vector3.up), sideAxis);
                initialSideSign = sideDot >= 0f ? 1f : -1f;
                hasInitialSideSign = true;
            }

            Vector3 sideDirection = sideAxis * initialSideSign;

            Vector3 forwardOnPlane = -sideDirection;
            if (forwardOnPlane.sqrMagnitude < 0.0001f) {
                forwardOnPlane = Vector3.forward;
            }

            transform.rotation = Quaternion.LookRotation(forwardOnPlane.normalized, Vector3.up);
            transform.position = center - transform.forward;
            desiredCenterViewport = GetDesiredCenterViewport();
            SnapCameraToBattleFraming();

            center = GetPlayersCenter();
            Vector3 finalForward = Vector3.ProjectOnPlane(center - transform.position, Vector3.up);
            if (finalForward.sqrMagnitude < 0.0001f) {
                finalForward = -sideDirection;
            }

            transform.rotation = Quaternion.LookRotation(finalForward.normalized, Vector3.up);
            firstRoundStartPosition = transform.position;
            firstRoundStartRotation = transform.rotation;
            hasFirstRoundStartPose = true;
        }

        private Vector2 GetDesiredCenterViewport() {
            return new Vector2(
                Mathf.Clamp01(0.5f + centerViewportOffset.x),
                Mathf.Clamp01(0.5f + centerViewportOffset.y));
        }

        private void SnapCameraToBattleFraming() {
            transform.position = GetBattleFramingPosition(transform.rotation);
        }

        private Vector3 GetBattleFramingPosition(Quaternion rotation) {
            Vector3 center = GetPlayersCenter();
            var inverseRotation = Quaternion.Inverse(rotation);
            Vector3 camSpace0 = inverseRotation * (GetPlayerCenterPosition(0) - center);
            Vector3 camSpace1 = inverseRotation * (GetPlayerCenterPosition(1) - center);
            float tanHalfVertical = Mathf.Max(0.0001f, Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float targetDepth = CalculateTargetDepthToFitPlayers(camSpace0, camSpace1, tanHalfVertical);
            float desiredLocalX = (desiredCenterViewport.x - 0.5f) * 2f * targetDepth * tanHalfVertical * stageCamera.aspect;
            float desiredLocalY = (desiredCenterViewport.y - 0.5f) * 2f * targetDepth * tanHalfVertical;
            return center - rotation * new Vector3(desiredLocalX, desiredLocalY, targetDepth);
        }

        private Vector3 GetPlayerCenterPosition(int playerId) {
            if (playerCenterPositionResolver == null) {
                throw new InvalidOperationException("StageCamera resolver is not configured. Call SetPlayerCenterPositionResolver from presenter before camera flow starts.");
            }

            return playerCenterPositionResolver(playerId);
        }

        private Vector3 GetInferredCamPosition1() {
            Vector3 player0Center = GetPlayerCenterPosition(0);
            Vector3 player1Center = GetPlayerCenterPosition(1);
            Vector3 camOffsetFromPlayer0 = camTransform0.position - player0Center;
            Vector3 mirroredOffset = new Vector3(-camOffsetFromPlayer0.x, camOffsetFromPlayer0.y, camOffsetFromPlayer0.z);
            return player1Center + mirroredOffset;
        }

    }
}
