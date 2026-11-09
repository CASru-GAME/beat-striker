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

            fillElapsedSeconds = Mathf.Min(fillDurationSeconds, fillElapsedSeconds + Time.deltaTime);
            currentRatio = fillElapsedSeconds >= fillDurationSeconds
                ? targetRatio
                : Mathf.Lerp(fillStartRatio, targetRatio, fillElapsedSeconds / fillDurationSeconds);
            ApplyScale(currentRatio);
            ApplyFullMaterial(currentRatio >= 1f);
        }

        public void SetSpecialRatio(float ratio) {
            var clampedRatio = Mathf.Clamp01(ratio);
            if (clampedRatio == targetRatio) return;

            targetRatio = clampedRatio;
            fillElapsedSeconds = 0f;
            if (clampedRatio < currentRatio) {
                currentRatio = clampedRatio;
                ApplyScale(currentRatio);
                ApplyFullMaterial(false);
            }
            fillStartRatio = currentRatio;
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

            if (!isFull) return;

            buttonGuideMotion.Play();
            if (fullFillSound) fullFillSound.PlayAtApp();
        }
    }
}
