using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Alice {
    public class AliceSpecialBarView : MonoBehaviour {
        [SerializeField] Transform specialBar;
        [SerializeField] Graphic specialBarGraphic;
        [SerializeField] Material normalMaterial;
        [SerializeField] Material fullMaterial;
        [FormerlySerializedAs("fillSmoothSpeed")]
        [SerializeField, Min(0f)] float fillDurationSeconds = 0.25f;
        [SerializeField] AudioClip fullFillSound;
        [SerializeField] ButtonGuideStartOffsetMotion buttonGuideMotion;

        float currentRatio;
        float targetRatio;
        float fillStartRatio;
        float fillElapsedSeconds;
        bool isFull;
        bool materialInitialized;
        float lastSetRatio;

        void Awake() {
            currentRatio = 0f;
            targetRatio = currentRatio;
            fillStartRatio = currentRatio;
            fillElapsedSeconds = 0f;
            ApplyScale(currentRatio);
            ApplyFullMaterial(false);
        }

        void Update() {
            if (targetRatio <= currentRatio) return;

            if (fillDurationSeconds <= 0f) {
                currentRatio = targetRatio;
                ApplyScale(currentRatio);
                ApplyFullMaterial(currentRatio >= 1f);
                return;
            }

            fillElapsedSeconds = Mathf.Min(fillDurationSeconds, fillElapsedSeconds + Time.deltaTime);
            var progress = Mathf.Clamp01(fillElapsedSeconds / fillDurationSeconds);
            currentRatio = Mathf.Lerp(fillStartRatio, targetRatio, progress);
            ApplyScale(currentRatio);

            if (Mathf.Approximately(currentRatio, targetRatio)) {
                ApplyFullMaterial(currentRatio >= 1f);
            }
        }

        public void SetSpecialRatio(float ratio) {
            var clampedRatio = Mathf.Clamp01(ratio);
            var previousRatio = currentRatio;

            if (clampedRatio < currentRatio) {
                currentRatio = clampedRatio;
                ApplyScale(currentRatio);
            }

            targetRatio = clampedRatio;
            fillStartRatio = currentRatio;
            fillElapsedSeconds = 0f;

            lastSetRatio = clampedRatio;

            if (clampedRatio < previousRatio) {
                ApplyFullMaterial(clampedRatio >= 1f);
            }

            if (clampedRatio >= 1f && currentRatio >= 1f) {
                ApplyFullMaterial(true);
            }
        }

        void ApplyScale(float ratio) {
            specialBar.localScale = new Vector3(
                ratio,
                specialBar.localScale.y,
                specialBar.localScale.z
            );
        }

        void ApplyFullMaterial(bool full) {
            if (materialInitialized && isFull == full) return;

            isFull = full;
            materialInitialized = true;
            specialBarGraphic.material = isFull ? fullMaterial : normalMaterial;

            if(fullFillSound && lastSetRatio > 0.5f && lastSetRatio < 1f) {
                AudioSource.PlayClipAtPoint(fullFillSound, Camera.main.transform.position);
                buttonGuideMotion.Play();
            }
        }
    }
}
