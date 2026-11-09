using System;
using System.Threading;
using UnityEngine;

namespace Alice {
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class BootCreditsView : MonoBehaviour {
        [Serializable]
        class CreditPage {
            [SerializeField] CanvasGroup content;
            [SerializeField, Min(0f)] float displaySeconds = 4f;

            public CanvasGroup Content => content;
            public float DisplaySeconds => displaySeconds;
        }

        [SerializeField] CanvasGroup overlay;
        [SerializeField] CreditPage[] pages;
        [SerializeField, Min(0f)] float textFadeSeconds = 0.35f;
        [SerializeField, Min(0f)] float revealBootSeconds = 0.65f;

        public async Awaitable PlayAsync(CancellationToken cancellationToken) {
            gameObject.SetActive(true);
            foreach (var page in pages) {
                await FadeAsync(page.Content, 1f, textFadeSeconds, cancellationToken);
                await HoldAsync(page.DisplaySeconds, cancellationToken);
                await FadeAsync(page.Content, 0f, textFadeSeconds, cancellationToken);
            }

            await FadeAsync(overlay, 0f, revealBootSeconds, cancellationToken);
            gameObject.SetActive(false);
        }

        static async Awaitable FadeAsync(CanvasGroup group, float target, float seconds, CancellationToken cancellationToken) {
            var start = group.alpha;
            var elapsed = 0f;
            while (elapsed < seconds) {
                await Awaitable.NextFrameAsync(cancellationToken);
                elapsed += FrameSeconds();
                group.alpha = Mathf.SmoothStep(start, target, elapsed / seconds);
            }
            group.alpha = target;
        }

        static async Awaitable HoldAsync(float seconds, CancellationToken cancellationToken) {
            for (var elapsed = 0f; elapsed < seconds; elapsed += FrameSeconds()) {
                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        static float FrameSeconds() => Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);
    }
}
