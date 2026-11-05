using R3;
using UnityEngine;

namespace Alice {
    public class RankingButtonHover : MonoBehaviour {
        [SerializeField] ActionEmitter emitter;
        [SerializeField] RectTransform target;
        [SerializeField] Vector3 hoverScale = new(1.04f, 1.06f, 1);
        [SerializeField, Min(0)] float duration = 0.12f;
        Vector3 originalScale;

        void Awake() {
            originalScale = target.localScale;
            emitter.OnHoverEvent.Subscribe(_ => Animate(true)).AddTo(this);
            emitter.OnHoverExitEvent.Subscribe(_ => Animate(false)).AddTo(this);
        }

        void Animate(bool hovered) {
            LeanTween.cancel(target.gameObject);
            LeanTween.scale(target.gameObject, hovered ? Vector3.Scale(originalScale, hoverScale) : originalScale, duration)
                .setIgnoreTimeScale(true).setEaseOutCubic();
        }

        void OnDisable() {
            LeanTween.cancel(target.gameObject);
            target.localScale = originalScale;
        }
    }
}
