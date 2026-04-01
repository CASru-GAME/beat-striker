using UnityEngine;

namespace Alice {
    public class AliceHpBarUI : MonoBehaviour {
        [SerializeField] Transform hpBar;
        [SerializeField] Transform damageBar;
        [SerializeField] float damageBarDelay = 0.3f;
        [SerializeField] float damageBarDuration = 0.3f;
        float previousHpRatio = 1f;
        bool initialized;

        void Start() {
            damageBar.localScale = new Vector3(1f, damageBar.localScale.y, damageBar.localScale.z);
        }

        public void SetHpRatio(float hpRatio) {
            var currentHpRatio = Mathf.Clamp01(hpRatio);
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
    }
}
