using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class SpecialCommandView : MonoBehaviour {
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Image strikerIcon;
        [SerializeField] RectTransform title;
        [SerializeField] RectTransform titleShadow;
        [SerializeField] RectTransform portrait;
        [SerializeField] RectTransform ribbon;
        [SerializeField] RectTransform[] speedLines;
        [SerializeField] CanvasGroup impactFlash;
        [SerializeField] RectTransform flashRect;

        [Header("Impact Motion")]
        [SerializeField, Min(0.01f)] float enterSeconds = 0.22f;
        [SerializeField, Min(0f)] float holdSeconds = 0.48f;
        [SerializeField, Min(0.01f)] float exitSeconds = 0.18f;
        [SerializeField, Min(0f)] float impactSeconds = 0.09f;
        [SerializeField, Min(0f)] float shakeDistance = 5f;
        [SerializeField, Min(0f)] float lineTravel = 160f;
        [SerializeField, Min(1f)] float titleStartScale = 2.4f;
        [SerializeField, Min(0f)] float travelPadding = 80f;

        Vector2 restPosition;
        Vector2 titlePosition;
        Vector2 portraitPosition;
        Vector2[] linePositions;
        Vector3 restScale;
        bool initialized;
        int tweenId = -1;

        void Awake() => HideImmediately();

        void EnsureInitialized() {
            if (initialized) return;
            restPosition = panel.anchoredPosition;
            restScale = panel.localScale;
            titlePosition = title.anchoredPosition;
            portraitPosition = portrait.anchoredPosition;
            linePositions = new Vector2[speedLines.Length];
            for (var i = 0; i < speedLines.Length; i++) linePositions[i] = speedLines[i].anchoredPosition;
            initialized = true;
        }

        public void Play(Sprite sprite) {
            HideImmediately();
            strikerIcon.sprite = sprite;
            panel.gameObject.SetActive(true);
            ApplyFrame(0f);
            var duration = enterSeconds + holdSeconds + exitSeconds;
            tweenId = LeanTween.value(gameObject, 0f, duration, duration)
                .setEase(LeanTweenType.linear)
                .setOnUpdate((float elapsed) => ApplyFrame(elapsed))
                .setOnComplete(HideImmediately).id;
        }

        // A single timeline keeps the portrait, type impact and streaks in sync.
        void ApplyFrame(float elapsed) {
            EnsureInitialized();
            var enter = Mathf.Max(0.01f, enterSeconds);
            var exit = Mathf.Max(0.01f, exitSeconds);
            var arrival = Mathf.Clamp01(elapsed / enter);
            var outProgress = Mathf.Clamp01((elapsed - enter - holdSeconds) / exit);
            var punch = Mathf.Clamp01(elapsed / (enter * 0.72f));
            var recoil = Mathf.Sin(punch * Mathf.PI) * (1f - punch);
            var impactTime = elapsed - enter * 0.72f;
            var shake = impactTime < 0f ? 0f : Mathf.Clamp01(1f - impactTime / Mathf.Max(0.01f, impactSeconds));
            var distance = ((RectTransform)panel.parent).rect.width * 0.5f + panel.rect.width * 0.5f + travelPadding;

            canvasGroup.alpha = Mathf.Clamp01(arrival * 5f) * (1f - outProgress * outProgress);
            panel.anchoredPosition = restPosition + new Vector2(distance * outProgress * outProgress * outProgress,
                Mathf.Sin(impactTime * 130f) * shakeDistance * shake);
            panel.localScale = Vector3.Scale(restScale, new Vector3(1f + outProgress * 0.35f, 1f - outProgress * 0.9f, 1f));
            ribbon.localScale = new Vector3(EaseOut(arrival), Mathf.Lerp(0.08f, 1f, Mathf.Clamp01(arrival * 2f)), 1f);
            title.localScale = Vector3.one * (Mathf.Lerp(titleStartScale, 1f, EaseOut(punch)) - recoil * 0.13f);
            title.anchoredPosition = titlePosition + Vector2.right * ((1f - EaseOut(punch)) * 65f);
            titleShadow.localScale = title.localScale;
            titleShadow.anchoredPosition = title.anchoredPosition + new Vector2(4f, -4f);
            portrait.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, EaseOut(Mathf.Clamp01(arrival * 1.7f)));
            portrait.anchoredPosition = portraitPosition + Vector2.up * ((1f - EaseOut(arrival)) * 38f);
            for (var i = 0; i < speedLines.Length; i++) {
                var progress = Mathf.Clamp01((elapsed - i * enter * 0.12f) / (enter + holdSeconds));
                speedLines[i].anchoredPosition = linePositions[i] + Vector2.right * ((EaseOut(progress) - 0.75f) * lineTravel * (i % 2 == 0 ? 1f : -1f));
                speedLines[i].localScale = new Vector3(Mathf.Clamp01(arrival * 3f), 1f, 1f);
            }
            // A narrow light blade expands on impact, then disappears quickly.
            impactFlash.alpha = shake * 0.85f;
            flashRect.localScale = new Vector3(1f + (1f - shake) * 1.5f, Mathf.Max(0.02f, shake), 1f);
        }

        static float EaseOut(float value) => 1f - Mathf.Pow(1f - Mathf.Clamp01(value), 3f);

        public void HideImmediately() {
            EnsureInitialized();
            if (tweenId >= 0) {
                LeanTween.cancel(tweenId);
                tweenId = -1;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            impactFlash.alpha = 0f;
            panel.anchoredPosition = restPosition;
            panel.localScale = restScale;
            title.anchoredPosition = titlePosition;
            title.localScale = Vector3.one;
            titleShadow.localScale = Vector3.one;
            titleShadow.anchoredPosition = titlePosition + new Vector2(4f, -4f);
            portrait.anchoredPosition = portraitPosition;
            portrait.localScale = Vector3.one;
            ribbon.localScale = Vector3.one;
            for (var i = 0; i < speedLines.Length; i++) {
                speedLines[i].anchoredPosition = linePositions[i];
                speedLines[i].localScale = Vector3.one;
            }
            panel.gameObject.SetActive(false);
        }

        void OnDisable() => HideImmediately();
    }
}
