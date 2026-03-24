using UnityEngine;
using System.Collections;
using R3;
using Core.App.Types;
using VContainer;

namespace Alice {
    [RequireComponent(typeof(Camera))]
    public class StageCamera : MonoBehaviour {
        [Header("Transforms")]
        [SerializeField] private Transform camTransform0;
        [SerializeField] private Transform playerTransform0;
        [SerializeField] private Transform camTransform1;
        [SerializeField] private Transform playerTransform1;
        [SerializeField] private Transform camTransformFinal;

        [Header("Settings")]
        [SerializeField] private float forwardDistance = 2f;
        [SerializeField] private float forwardDuration = 1f;
        [SerializeField] private float orbitDuration = 3f;
        [SerializeField] private float orbitAngle = -20f;
        [SerializeField] private float outroDistance = 3f;
        [SerializeField] private float outroDuration = 1f;
        [SerializeField] private float outroWaitDuration = 3f;
        [SerializeField] private float ratioDistanceMin = 2f;
        [SerializeField] private float ratioDistanceMax = 12f;
        [SerializeField, Range(0.1f, 0.95f)] private float nearDistanceViewportRatio = 0.28f;
        [SerializeField, Range(0.1f, 0.95f)] private float farDistanceViewportRatio = 0.4f;
        [SerializeField] private float zoomSmoothTime = 0.2f;
        [SerializeField] private float centerSmoothTime = 0.15f;
        [SerializeField] private float minFov = 20f;
        [SerializeField] private float maxFov = 70f;

        IBattleFlow battleFlow;
        private CompositeDisposable disposables = new();
        private Camera stageCamera;
        private float zoomVelocity;
        private Vector3 centerMoveVelocity;
        private Vector2 desiredCenterViewport;
        private bool isBattleZoomActive;

        [Inject]
        public void Construct(IBattleFlow battleFlow) {
            this.battleFlow = battleFlow;
        }

        void Start() {
            stageCamera = GetComponent<Camera>();

            battleFlow.PrepareBattle();

            battleFlow.OutroStarted
                .Subscribe(_ => OnOutro())
                .AddTo(disposables);

            StartCoroutine(StartCameraSequence());
        }

        void Update() {
            if (!isBattleZoomActive) {
                return;
            }

            UpdateBattleZoom();
        }

        void OnDestroy() {
            disposables.Dispose();
        }

        private IEnumerator StartCameraSequence() {
            Vector3 startPosition = transform.position;
            Vector3 forwardPosition = startPosition + transform.forward * forwardDistance;

            // 少し前に進む
            float elapsedTime = 0f;
            while (elapsedTime < forwardDuration) {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / forwardDuration;
                transform.position = Vector3.Lerp(startPosition, forwardPosition, t);
                yield return null;
            }

            // transform0の位置にワープしてplayerTransform0を見ながら公転
            transform.position = camTransform0.position;
            LookAt(playerTransform0.transform);
            yield return StartCoroutine(OrbitAroundTarget(playerTransform0.transform, orbitDuration, orbitAngle));

            // transform1の位置にワープしてplayerTransform1を見ながら公転
            transform.position = camTransform1.position;
            LookAt(playerTransform1.transform);
            yield return StartCoroutine(OrbitAroundTarget(playerTransform1.transform, orbitDuration, -orbitAngle));

            // transformFinalにワープ
            transform.SetPositionAndRotation(camTransformFinal.position, camTransformFinal.rotation);
            desiredCenterViewport = stageCamera.WorldToViewportPoint(GetPlayersCenter());
            isBattleZoomActive = true;

            battleFlow?.NotifyIntroAnimationFinished();
        }

        private IEnumerator OrbitAroundTarget(Transform target, float duration, float angle) {
            Vector3 startOffset = transform.position - target.position;
            float elapsedTime = 0f;

            while (elapsedTime < duration) {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                float currentAngle = Mathf.Lerp(0f, angle, t);

                // 現在のtarget位置を中心に回転した位置を計算
                Vector3 pivot = target.position;
                Quaternion rotation = Quaternion.Euler(0, currentAngle, 0);
                Vector3 newPos = pivot + rotation * startOffset;

                transform.position = newPos;
                LookAt(target);

                yield return null;
            }
        }

        private void LookAt(Transform target) {
            if (target != null) {
                transform.LookAt(target);
            }
        }

        void OnOutro() {
            isBattleZoomActive = false;
            var winner = new PlayerId(0);
            Transform targetTransform = winner.value == 0 ? playerTransform0 : playerTransform1;
            StartCoroutine(MoveToWinner(targetTransform, winner));
        }

        private void UpdateBattleZoom() {
            MoveParallelToPlayersCenter();

            float targetFov = CalculateTargetFov(playerTransform0.position, playerTransform1.position);
            stageCamera.fieldOfView = Mathf.SmoothDamp(
                stageCamera.fieldOfView,
                targetFov,
                ref zoomVelocity,
                zoomSmoothTime);
        }

        private Vector3 GetPlayersCenter() {
            return (playerTransform0.position + playerTransform1.position) * 0.5f;
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

        private float CalculateTargetFov(Vector3 player0, Vector3 player1) {
            Vector3 camSpace0 = transform.InverseTransformPoint(player0);
            Vector3 camSpace1 = transform.InverseTransformPoint(player1);

            float horizontalDistance = Mathf.Abs(camSpace1.x - camSpace0.x);
            float averageDepth = Mathf.Max(0.01f, (camSpace0.z + camSpace1.z) * 0.5f);
            float ratio = CalculateDistanceBasedRatio(horizontalDistance);

            float targetHorizontalFov = 2f * Mathf.Atan(horizontalDistance / (2f * averageDepth * ratio));
            float targetVerticalFov = 2f * Mathf.Atan(Mathf.Tan(targetHorizontalFov * 0.5f) / stageCamera.aspect);
            float targetFovDeg = targetVerticalFov * Mathf.Rad2Deg;

            return Mathf.Clamp(targetFovDeg, minFov, maxFov);
        }

        private float CalculateDistanceBasedRatio(float playerDistance) {
            float t = Mathf.InverseLerp(ratioDistanceMin, ratioDistanceMax, playerDistance);
            return Mathf.Lerp(nearDistanceViewportRatio, farDistanceViewportRatio, t);
        }

        private IEnumerator MoveToWinner(Transform target, PlayerId winner) {
            yield return new WaitForSeconds(outroWaitDuration);

            Vector3 startPosition = transform.position;
            Vector3 direction = (target.position - transform.position).normalized;
            Vector3 targetPosition = target.position - direction * outroDistance;

            float elapsedTime = 0f;

            while (elapsedTime < outroDuration) {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / outroDuration;
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                LookAt(target);
                yield return null;
            }

            transform.position = targetPosition;
            LookAt(target);

            yield return new WaitForSeconds(outroWaitDuration);
            battleFlow?.NotifyOutroAnimationFinished();
        }
    }
}