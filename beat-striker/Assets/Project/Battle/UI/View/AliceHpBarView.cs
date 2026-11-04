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
        bool damageBarAnimating;
        float damageBarDelayRemaining;
        float damageBarSpeed;

        void Update() {
            if (!damageBarAnimating || openingFillLocked) return;

            var animationDeltaTime = Mathf.Max(0f, Time.deltaTime - damageBarDelayRemaining);
            damageBarDelayRemaining = Mathf.Max(0f, damageBarDelayRemaining - Time.deltaTime);
            if (animationDeltaTime <= 0f) return;

            var ratio = damageBarDuration > 0f
                ? Mathf.MoveTowards(damageBar.localScale.x, previousHpRatio, damageBarSpeed * animationDeltaTime)
                : previousHpRatio;
            damageBar.localScale = new Vector3(ratio, damageBar.localScale.y, damageBar.localScale.z);
            if (ratio == previousHpRatio) damageBarAnimating = false;
        }

        void Start() {
            hpBar.localScale = new Vector3(0f, hpBar.localScale.y, hpBar.localScale.z);
            damageBar.localScale = new Vector3(0f, damageBar.localScale.y, damageBar.localScale.z);
        }

        public void ResetToZeroImmediately() {
            damageBarAnimating = false;
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

            var damageRatio = damageBar.localScale.x - currentHpRatio;
            if (damageRatio <= 0f) {
                damageBar.localScale = new Vector3(currentHpRatio, damageBar.localScale.y, damageBar.localScale.z);
                damageBarAnimating = false;
            } else {
                if (!damageBarAnimating) {
                    damageBarDelayRemaining = Mathf.Max(0f, damageBarDelay);
                    damageBarSpeed = 0f;
                    damageBarAnimating = true;
                }
                if (damageBarDuration > 0f) {
                    damageBarSpeed = Mathf.Max(damageBarSpeed, damageRatio / damageBarDuration);
                }
            }

            previousHpRatio = currentHpRatio;
        }

        public Task PlayOpeningFillAsync(float durationSeconds) {
            damageBarAnimating = false;
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
