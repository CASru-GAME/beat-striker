using UnityEngine;
using System.Threading.Tasks;

namespace Alice {
    public class AliceHpBarView : MonoBehaviour {
        [SerializeField] Transform hpBar;
        [SerializeField] Transform damageBar;
        [SerializeField] AudioClip openingFillSound;
        [SerializeField] float openingFillSoundVolume = 1f;
        [SerializeField] float damageBarDelay = 0.3f;
        [SerializeField] float damageBarDuration = 0.3f;
        float previousHpRatio = 1f;
        float pendingHpRatio = 1f;
        bool openingFillLocked;
        bool initialized;

        void Start() {
            hpBar.localScale = new Vector3(0f, hpBar.localScale.y, hpBar.localScale.z);
            damageBar.localScale = new Vector3(0f, damageBar.localScale.y, damageBar.localScale.z);
        }

        public void ResetToZeroImmediately() {
            LeanTween.cancel(hpBar.gameObject);
            LeanTween.cancel(damageBar.gameObject);

            hpBar.localScale = new Vector3(0f, hpBar.localScale.y, hpBar.localScale.z);
            damageBar.localScale = new Vector3(0f, damageBar.localScale.y, damageBar.localScale.z);
            previousHpRatio = 0f;
            pendingHpRatio = 1f;
            openingFillLocked = true;
            initialized = false;
        }

        public void ReleaseOpeningFillLock(float hpRatio) {
            openingFillLocked = false;
            previousHpRatio = hpRatio;
            pendingHpRatio = hpRatio;
            initialized = true;
        }

        public void SetHpRatio(float hpRatio) {
            var currentHpRatio = Mathf.Clamp01(hpRatio);
            if (openingFillLocked) {
                pendingHpRatio = currentHpRatio;
                return;
            }

            if (!initialized) {
                hpBar.localScale = new Vector3(currentHpRatio, hpBar.localScale.y, hpBar.localScale.z);
                damageBar.localScale = new Vector3(currentHpRatio, damageBar.localScale.y, damageBar.localScale.z);
                previousHpRatio = currentHpRatio;
                initialized = true;
                return;
            }

            if (Mathf.Abs(currentHpRatio - previousHpRatio) <= 0.001f) return;

            hpBar.localScale = new Vector3(currentHpRatio, hpBar.localScale.y, hpBar.localScale.z);

            damageBar.localScale = new Vector3(previousHpRatio, damageBar.localScale.y, damageBar.localScale.z);
            LeanTween.cancel(damageBar.gameObject);
            LeanTween.scaleX(damageBar.gameObject, currentHpRatio, damageBarDuration)
                .setDelay(damageBarDelay)
                .setEase(LeanTweenType.easeOutQuad);

            previousHpRatio = currentHpRatio;
        }

        public Task PlayOpeningFillAsync(float durationSeconds) {
            var completionSource = new TaskCompletionSource<bool>();
            var targetHpRatio = pendingHpRatio;
            openingFillSound.PlayAtApp(Vector3.zero, openingFillSoundVolume);

            LeanTween.cancel(hpBar.gameObject);
            LeanTween.cancel(damageBar.gameObject);

            hpBar.localScale = new Vector3(0f, hpBar.localScale.y, hpBar.localScale.z);
            damageBar.localScale = new Vector3(0f, damageBar.localScale.y, damageBar.localScale.z);

            LeanTween.scaleX(hpBar.gameObject, targetHpRatio, durationSeconds)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() => {
                    hpBar.localScale = new Vector3(targetHpRatio, hpBar.localScale.y, hpBar.localScale.z);
                    damageBar.localScale = new Vector3(targetHpRatio, damageBar.localScale.y, damageBar.localScale.z);
                    ReleaseOpeningFillLock(targetHpRatio);
                    completionSource.TrySetResult(true);
                });

            previousHpRatio = targetHpRatio;

            return completionSource.Task;
        }
    }
}
