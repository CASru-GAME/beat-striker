using UnityEngine;

namespace Alice {
    public class AliceHpBarUI : MonoBehaviour {
        AliceStrikerHub strikerHub;
        [SerializeField] Transform hpBar;
        [SerializeField] Transform damageBar;
        [SerializeField] float damageBarDelay = 0.3f;
        [SerializeField] float damageBarDuration = 0.3f;
        float previousHpRatio = 1f;
        bool initialized;

        public void Construct(AliceStrikerHub strikerHub) {
            this.strikerHub = strikerHub;
            var max = Mathf.Max(1f, strikerHub.MaxHitPoint);
            previousHpRatio = Mathf.Clamp01(strikerHub.CurrentHitPoint / max);
            initialized = true;
        }

        void Start() {
            damageBar.localScale = new Vector3(1f, damageBar.localScale.y, damageBar.localScale.z);
        }

        void Update() {
            if (!initialized) return;

            var max = Mathf.Max(1f, strikerHub.MaxHitPoint);
            var currentHpRatio = Mathf.Clamp01(strikerHub.CurrentHitPoint / max);

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
