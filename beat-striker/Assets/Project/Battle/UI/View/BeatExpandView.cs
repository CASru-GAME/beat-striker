using UnityEngine;

namespace Alice {
    public class BeatExpandView : MonoBehaviour {
        [SerializeField] RectTransform expandTarget;

        [Header("Animation")]
        [SerializeField] float expandScaleMultiplier = 1.08f;
        [SerializeField] float expandDuration = 0.04f;
        [SerializeField] float returnDuration = 0.08f;

        Vector3 settledScale;

        void Awake() {
            settledScale = expandTarget.localScale;
            if (settledScale.sqrMagnitude <= 0.000001f) {
                settledScale = Vector3.one;
            }
        }

        public void PlayBeatExpand() {
            LeanTween.cancel(expandTarget.gameObject);
            expandTarget.localScale = settledScale;

            var expandedScale = settledScale * expandScaleMultiplier;
            var expandTweenDuration = Mathf.Max(0.0001f, expandDuration);
            var returnTweenDuration = Mathf.Max(0.0001f, returnDuration);

            LeanTween.scale(expandTarget, expandedScale, expandTweenDuration)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() => {
                    LeanTween.scale(expandTarget, settledScale, returnTweenDuration)
                        .setEase(LeanTweenType.easeInQuad);
                });
        }

        void OnDisable() {
            LeanTween.cancel(expandTarget.gameObject);
            expandTarget.localScale = settledScale;
        }
    }
}