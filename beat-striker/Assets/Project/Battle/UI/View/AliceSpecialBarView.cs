using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class AliceSpecialBarView : MonoBehaviour {
        [SerializeField] Transform specialBar;
        [SerializeField] Graphic specialBarGraphic;
        [SerializeField] Material normalMaterial;
        [SerializeField] Material fullMaterial;
        [SerializeField] float fillSmoothSpeed = 4f;
        [SerializeField] AudioClip fullFillSound;
        [SerializeField] ButtonGuideStartOffsetMotion buttonGuideMotion;

        float currentRatio;
        float targetRatio;
        bool isFull;
        bool materialInitialized;
        float lastSetRatio;

        void Awake() {
            currentRatio = 0f;
            targetRatio = currentRatio;
            ApplyScale(currentRatio);
            ApplyFullMaterial(false);
        }

        void Update() {
            if (targetRatio <= currentRatio) return;

            currentRatio = Mathf.MoveTowards(currentRatio, targetRatio, fillSmoothSpeed * Time.deltaTime);
            ApplyScale(currentRatio);
        }

        public void SetSpecialRatio(float ratio) {
            var clampedRatio = Mathf.Clamp01(ratio);
            targetRatio = clampedRatio;

            if (clampedRatio < currentRatio) {
                currentRatio = clampedRatio;
                ApplyScale(currentRatio);
            }

            ApplyFullMaterial(clampedRatio >= 1f);
            lastSetRatio = clampedRatio;
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
