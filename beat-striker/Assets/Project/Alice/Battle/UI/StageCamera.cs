using UnityEngine;
using System.Collections;
using System;
using System.Threading.Tasks;
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
        [SerializeField] private float maxPlayersDistanceToFitOnScreen = 12f;
        [SerializeField] private AnimationCurve normalizedDistanceToDiagonalRatio = new AnimationCurve(
            new Keyframe(0f, 0.28f),
            new Keyframe(1f, 0.4f));
        [SerializeField] private float zoomSmoothTime = 0.2f;
        [SerializeField] private float centerSmoothTime = 0.15f;
        [SerializeField] private Vector2 centerViewportOffset = Vector2.zero;
        [SerializeField] private float shakeDamping = 18f;
        [SerializeField] private float attentionZoomDistance = 2.2f;
        [SerializeField] private float attentionZoomSmoothTime = 0.08f;
        [SerializeField] private float attentionReturnSmoothTime = 0.12f;

        private Camera stageCamera;
        private float cameraDepthVelocity;
        private Vector3 centerMoveVelocity;
        Vector3 shakeOffset;
        Vector3 shakeVelocity;
        Vector3 previousShakeOffset;
        private Vector2 desiredCenterViewport;
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
            public Quaternion BaseRotation;
            public Vector3 BasePosition;
            public Vector3 BaseForward;
            public Vector3 MoveVelocity;
            public Vector3 ReturnMoveVelocity;

            public void Reset() {
                RemainingSeconds = 0f;
                MoveVelocity = Vector3.zero;
                ReturnMoveVelocity = Vector3.zero;
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
        }

        sealed class IdleCameraState : CameraState {
            public IdleCameraState(StageCamera owner) : base(owner) { }

            public override void OnEnter() {
                owner.attentionRuntime.Reset();
            }
        }

        sealed class BattleCameraState : CameraState {
            public BattleCameraState(StageCamera owner) : base(owner) { }

            public override void OnEnter() {
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

                owner.attentionRuntime.BasePosition = owner.transform.position;
                owner.attentionRuntime.BaseRotation = owner.transform.rotation;
                owner.attentionRuntime.BaseForward = owner.transform.forward;
                owner.attentionRuntime.TargetPlayerId = playerId;
                owner.attentionRuntime.RemainingSeconds = durationSeconds;
                owner.attentionRuntime.MoveVelocity = Vector3.zero;
                owner.attentionRuntime.ReturnMoveVelocity = Vector3.zero;
                owner.SetCameraState(owner.attentionState);
            }
        }

        sealed class AttentionCameraState : CameraState {
            public AttentionCameraState(StageCamera owner) : base(owner) { }

            public override void OnUpdate() {
                owner.attentionRuntime.RemainingSeconds -= Time.deltaTime;
                var targetCenter = owner.GetPlayerCenterPosition(owner.attentionRuntime.TargetPlayerId);
                var targetPosition = targetCenter - owner.attentionRuntime.BaseForward * owner.attentionZoomDistance;
                owner.transform.position = Vector3.SmoothDamp(
                    owner.transform.position,
                    targetPosition,
                    ref owner.attentionRuntime.MoveVelocity,
                    owner.attentionZoomSmoothTime);
                owner.LookAt(targetCenter);

                if (owner.attentionRuntime.RemainingSeconds > 0f) {
                    return;
                }

                owner.attentionRuntime.RemainingSeconds = 0f;
                owner.attentionRuntime.MoveVelocity = Vector3.zero;
                owner.SetCameraState(owner.attentionReturnState);
            }

            public override void OnAttentionRequested(int playerId, float durationSeconds) {
                if (durationSeconds <= 0f) {
                    return;
                }

                owner.attentionRuntime.TargetPlayerId = playerId;
                owner.attentionRuntime.RemainingSeconds = durationSeconds;
                owner.attentionRuntime.MoveVelocity = Vector3.zero;
            }
        }

        sealed class AttentionReturnCameraState : CameraState {
            public AttentionReturnCameraState(StageCamera owner) : base(owner) { }

            public override void OnUpdate() {
                owner.transform.position = Vector3.SmoothDamp(
                    owner.transform.position,
                    owner.attentionRuntime.BasePosition,
                    ref owner.attentionRuntime.ReturnMoveVelocity,
                    owner.attentionReturnSmoothTime);
                owner.transform.rotation = Quaternion.Slerp(
                    owner.transform.rotation,
                    owner.attentionRuntime.BaseRotation,
                    1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, owner.attentionReturnSmoothTime)));

                bool reachedPosition = (owner.transform.position - owner.attentionRuntime.BasePosition).sqrMagnitude <= 0.0004f;
                bool reachedRotation = Quaternion.Angle(owner.transform.rotation, owner.attentionRuntime.BaseRotation) <= 0.2f;
                if (!reachedPosition || !reachedRotation) {
                    return;
                }

                owner.transform.SetPositionAndRotation(owner.attentionRuntime.BasePosition, owner.attentionRuntime.BaseRotation);
                owner.attentionRuntime.ReturnMoveVelocity = Vector3.zero;
                owner.SetCameraState(owner.battleState);
            }

            public override void OnAttentionRequested(int playerId, float durationSeconds) {
                if (durationSeconds <= 0f) {
                    return;
                }

                owner.attentionRuntime.TargetPlayerId = playerId;
                owner.attentionRuntime.RemainingSeconds = durationSeconds;
                owner.attentionRuntime.MoveVelocity = Vector3.zero;
                owner.attentionRuntime.ReturnMoveVelocity = Vector3.zero;
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
                yield return WaitForSecondsSkippable(owner.outroWaitDuration, CameraSequencePhase.OutroPreWait);
                owner.victoryPoseRequester?.Invoke(winnerPlayerId);

                Vector3 startPosition = owner.transform.position;
                Vector3 winnerCenterPosition = owner.GetPlayerCenterPosition(winnerPlayerId);
                Vector3 direction = (winnerCenterPosition - owner.transform.position).normalized;
                Vector3 targetPosition = winnerCenterPosition - direction * owner.outroDistance;

                SetPhase(CameraSequencePhase.OutroMove);
                float elapsedTime = 0f;

                while (elapsedTime < owner.outroDuration) {
                    if (ConsumeSkipIfRequested(CameraSequencePhase.OutroMove)) {
                        break;
                    }

                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / owner.outroDuration;
                    owner.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                    owner.LookAt(owner.GetPlayerCenterPosition(winnerPlayerId));
                    yield return null;
                }

                owner.transform.position = targetPosition;
                owner.LookAt(owner.GetPlayerCenterPosition(winnerPlayerId));

                yield return WaitForSecondsSkippable(owner.outroWaitDuration, CameraSequencePhase.OutroPostWait);
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

        void Update() {
            EnsureRuntimeInitialized();
            currentState?.OnUpdate();
        }

        void LateUpdate() {
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

        public void RequestAttention(int playerId, float durationSeconds) {
            EnsureRuntimeInitialized();
            currentState?.OnAttentionRequested(playerId, durationSeconds);
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

        void ApplyShakeOffset() {
            transform.position -= previousShakeOffset;

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
            Vector3 moveInWorld = transform.TransformVector(moveInCameraSpace);
            Vector3 targetPosition = transform.position + moveInWorld;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref centerMoveVelocity,
                centerSmoothTime);
        }

        private void MoveCameraForwardToFitPlayers() {
            Vector3 camSpace0 = transform.InverseTransformPoint(GetPlayerCenterPosition(0));
            Vector3 camSpace1 = transform.InverseTransformPoint(GetPlayerCenterPosition(1));

            Vector2 playersDeltaInCameraPlane = new Vector2(
                camSpace1.x - camSpace0.x,
                camSpace1.y - camSpace0.y);
            float playersDistance = playersDeltaInCameraPlane.magnitude;
            float currentDepth = Mathf.Max(0.01f, (camSpace0.z + camSpace1.z) * 0.5f);
            float diagonalDistanceRatio = CalculateDiagonalDistanceRatio(playersDistance);
            float diagonalScale = Mathf.Sqrt(stageCamera.aspect * stageCamera.aspect + 1f);
            float tanHalfVertical = Mathf.Max(0.0001f, Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float targetDepth = playersDistance / (2f * tanHalfVertical * diagonalDistanceRatio * diagonalScale);

            float smoothedDepth = Mathf.SmoothDamp(
                currentDepth,
                targetDepth,
                ref cameraDepthVelocity,
                zoomSmoothTime);
            float depthDelta = currentDepth - smoothedDepth;

            transform.position += transform.forward * depthDelta;
        }

        private float CalculateDiagonalDistanceRatio(float playersDistance) {
            float safeMaxDistance = Mathf.Max(0.01f, maxPlayersDistanceToFitOnScreen);
            float normalizedDistance = Mathf.Clamp01(playersDistance / safeMaxDistance);
            float diagonalDistanceRatio = normalizedDistanceToDiagonalRatio.Evaluate(normalizedDistance);
            return Mathf.Clamp(diagonalDistanceRatio, 0.05f, 0.95f);
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
            Vector3 camSpace0 = transform.InverseTransformPoint(GetPlayerCenterPosition(0));
            Vector3 camSpace1 = transform.InverseTransformPoint(GetPlayerCenterPosition(1));
            Vector2 playersDeltaInCameraPlane = new Vector2(
                camSpace1.x - camSpace0.x,
                camSpace1.y - camSpace0.y);
            float playersDistance = playersDeltaInCameraPlane.magnitude;
            float currentDepth = Mathf.Max(0.01f, (camSpace0.z + camSpace1.z) * 0.5f);
            float diagonalDistanceRatio = CalculateDiagonalDistanceRatio(playersDistance);
            float diagonalScale = Mathf.Sqrt(stageCamera.aspect * stageCamera.aspect + 1f);
            float tanHalfVertical = Mathf.Max(0.0001f, Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float targetDepth = playersDistance / (2f * tanHalfVertical * diagonalDistanceRatio * diagonalScale);
            float depthDelta = currentDepth - targetDepth;
            transform.position += transform.forward * depthDelta;

            Vector3 center = GetPlayersCenter();
            Vector3 centerInCameraSpace = transform.InverseTransformPoint(center);
            float depth = Mathf.Max(0.01f, centerInCameraSpace.z);
            float halfVerticalFovRad = stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalfVerticalForCenter = Mathf.Tan(halfVerticalFovRad);
            float tanHalfHorizontal = tanHalfVerticalForCenter * stageCamera.aspect;
            float desiredLocalX = (desiredCenterViewport.x - 0.5f) * 2f * depth * tanHalfHorizontal;
            float desiredLocalY = (desiredCenterViewport.y - 0.5f) * 2f * depth * tanHalfVerticalForCenter;
            Vector3 moveInCameraSpace = new Vector3(
                centerInCameraSpace.x - desiredLocalX,
                centerInCameraSpace.y - desiredLocalY,
                0f);
            Vector3 moveInWorld = transform.TransformVector(moveInCameraSpace);
            transform.position += moveInWorld;
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