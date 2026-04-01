using UnityEngine;
using System.Collections;
using System;
using System.Threading.Tasks;
using Core.App.Types;

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

        private Camera stageCamera;
        private float cameraDepthVelocity;
        private Vector3 centerMoveVelocity;
        private Vector2 desiredCenterViewport;
        private bool isBattleZoomActive;
        bool isSkipRequested;
        CameraSequencePhase currentSequencePhase;
        TaskCompletionSource<bool> introCompletionSource;
        TaskCompletionSource<bool> outroCompletionSource;
        Func<int, Vector3> playerCenterPositionResolver;
        Action<int> introPoseRequester;
        Action<int> victoryPoseRequester;
        Vector3 initialCameraPosition;
        float initialSideSign;
        bool hasInitialSideSign;
        Vector3 firstRoundStartPosition;
        Quaternion firstRoundStartRotation;
        bool hasFirstRoundStartPose;

        enum CameraSequencePhase {
            None,
            IntroForward,
            IntroPlayer0,
            IntroPlayer1,
            OutroPreWait,
            OutroMove,
            OutroPostWait,
        }

        void Awake() {
            initialCameraPosition = transform.position;
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
            stageCamera = GetComponent<Camera>();
        }

        void Update() {
            if (!isBattleZoomActive) {
                return;
            }

            UpdateBattleZoom();
        }

        public Task PresentIntroAsync() {
            introCompletionSource?.TrySetCanceled();
            introCompletionSource = new TaskCompletionSource<bool>();
            isSkipRequested = false;
            currentSequencePhase = CameraSequencePhase.None;
            StopAllCoroutines();
            StartCoroutine(StartCameraSequence());
            return introCompletionSource.Task;
        }

        private IEnumerator StartCameraSequence() {
            Vector3 startPosition = transform.position;
            Vector3 forwardPosition = startPosition + transform.forward * forwardDistance;

            // 少し前に進む
            currentSequencePhase = CameraSequencePhase.IntroForward;
            float elapsedTime = 0f;
            while (elapsedTime < forwardDuration) {
                if (ConsumeSkipIfRequested(CameraSequencePhase.IntroForward)) {
                    break;
                }

                elapsedTime += Time.deltaTime;
                float t = elapsedTime / forwardDuration;
                transform.position = Vector3.Lerp(startPosition, forwardPosition, t);
                yield return null;
            }
            transform.position = forwardPosition;

            // transform0の位置にワープしてplayerTransform0を見ながら公転
            currentSequencePhase = CameraSequencePhase.IntroPlayer0;
            transform.position = camTransform0.position;
            introPoseRequester?.Invoke(0);
            LookAt(GetPlayerCenterPosition(0));
            yield return OrbitAroundTarget(0, orbitDuration, orbitAngle, CameraSequencePhase.IntroPlayer0);

            // transform1の位置にワープしてplayerTransform1を見ながら公転
            currentSequencePhase = CameraSequencePhase.IntroPlayer1;
            transform.position = GetInferredCamPosition1();
            introPoseRequester?.Invoke(1);
            LookAt(GetPlayerCenterPosition(1));
            yield return OrbitAroundTarget(1, orbitDuration, -orbitAngle, CameraSequencePhase.IntroPlayer1);

            InitializeBattleStartCameraPose();
            isBattleZoomActive = false;
            currentSequencePhase = CameraSequencePhase.None;

            introCompletionSource?.TrySetResult(true);
        }

        private IEnumerator OrbitAroundTarget(int playerId, float duration, float angle, CameraSequencePhase phase) {
            Vector3 pivot = GetPlayerCenterPosition(playerId);
            Vector3 startOffset = transform.position - pivot;
            float elapsedTime = 0f;

            while (elapsedTime < duration) {
                if (ConsumeSkipIfRequested(phase)) {
                    break;
                }

                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                float currentAngle = Mathf.Lerp(0f, angle, t);

                // 現在のtarget位置を中心に回転した位置を計算
                pivot = GetPlayerCenterPosition(playerId);
                Quaternion rotation = Quaternion.Euler(0, currentAngle, 0);
                Vector3 newPos = pivot + rotation * startOffset;

                transform.position = newPos;
                LookAt(pivot);

                yield return null;
            }
        }

        private void LookAt(Vector3 targetPosition) {
            transform.LookAt(targetPosition);
        }

        public void PresentRoundPlayableStart() {
            isBattleZoomActive = true;
            cameraDepthVelocity = 0f;
            centerMoveVelocity = Vector3.zero;
        }

        public void ResetRoundCamera() {
            StopAllCoroutines();
            isBattleZoomActive = false;
            InitializeBattleStartCameraPose();
            cameraDepthVelocity = 0f;
            centerMoveVelocity = Vector3.zero;
        }

        public void PresentRoundFinish() {
            isBattleZoomActive = false;
        }

        public void PresentBattleFinish() {
            isBattleZoomActive = false;
        }

        public Task PresentOutroAsync(PlayerId winner) {
            outroCompletionSource?.TrySetCanceled();
            outroCompletionSource = new TaskCompletionSource<bool>();
            isBattleZoomActive = false;
            isSkipRequested = false;
            currentSequencePhase = CameraSequencePhase.None;
            StopAllCoroutines();
            StartCoroutine(MoveToWinner(winner.value));
            return outroCompletionSource.Task;
        }

        public void RequestSequenceSkip() {
            isSkipRequested = true;
        }

        bool ConsumeSkipIfRequested(CameraSequencePhase expectedPhase) {
            if (!isSkipRequested || currentSequencePhase != expectedPhase) {
                return false;
            }

            isSkipRequested = false;
            return true;
        }

        IEnumerator WaitForSecondsSkippable(float duration, CameraSequencePhase phase) {
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

        private void UpdateBattleZoom() {
            MoveCameraForwardToFitPlayers();
            MoveParallelToPlayersCenter();
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

        private IEnumerator MoveToWinner(int winnerPlayerId) {
            yield return WaitForSecondsSkippable(outroWaitDuration, CameraSequencePhase.OutroPreWait);
            victoryPoseRequester?.Invoke(winnerPlayerId);

            Vector3 startPosition = transform.position;
            Vector3 winnerCenterPosition = GetPlayerCenterPosition(winnerPlayerId);
            Vector3 direction = (winnerCenterPosition - transform.position).normalized;
            Vector3 targetPosition = winnerCenterPosition - direction * outroDistance;

            currentSequencePhase = CameraSequencePhase.OutroMove;
            float elapsedTime = 0f;

            while (elapsedTime < outroDuration) {
                if (ConsumeSkipIfRequested(CameraSequencePhase.OutroMove)) {
                    break;
                }

                elapsedTime += Time.deltaTime;
                float t = elapsedTime / outroDuration;
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                LookAt(GetPlayerCenterPosition(winnerPlayerId));
                yield return null;
            }

            transform.position = targetPosition;
            LookAt(GetPlayerCenterPosition(winnerPlayerId));

            yield return WaitForSecondsSkippable(outroWaitDuration, CameraSequencePhase.OutroPostWait);
            currentSequencePhase = CameraSequencePhase.None;
            outroCompletionSource?.TrySetResult(true);
        }
    }
}