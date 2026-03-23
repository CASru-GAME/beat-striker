using UnityEngine;
using System.Collections;
using R3;
using Core.App.Types;
using VContainer;

namespace Alice {
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

        IBattleFlow battleFlow;
        private CompositeDisposable disposables = new();

        [Inject]
        public void Construct(IBattleFlow battleFlow) {
            this.battleFlow = battleFlow;
        }

        void Start() {
            battleFlow.OutroStarted
                .Subscribe(_ => OnOutro())
                .AddTo(disposables);

            StartCoroutine(StartCameraSequence());
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
            var winner = new PlayerId(0);
            Transform targetTransform = winner.value == 0 ? playerTransform0 : playerTransform1;
            StartCoroutine(MoveToWinner(targetTransform, winner));
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