using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Alice {
    public class TrialAnnouncementView : MonoBehaviour {
        [SerializeField] GameObject content;
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform band;
        [SerializeField] TMP_Text grade;
        [SerializeField] CanvasGroup flash;
        [SerializeField] AudioClip impactSound;
        [SerializeField, Min(.01f)] float impactDuration = .22f;
        [SerializeField, Min(.01f)] float holdDuration = 1.1f;
        [SerializeField, Min(.01f)] float exitDuration = .2f;
        [SerializeField] float startScale = 2.6f;
        [SerializeField] float shakeAmplitude = 13f;
        int targetGrade = -1;
        public void Configure(int value) { targetGrade = value; content.SetActive(false); }
        public async Task PlayAsync(float extraHoldDuration) {
            if (targetGrade < 0) return;
            var cancellation = destroyCancellationToken;
            var start = Time.unscaledTime;
            var sounded = false;
            var hold = holdDuration + extraHoldDuration;
            try {
                while (Time.unscaledTime - start < impactDuration + hold + exitDuration) {
                    cancellation.ThrowIfCancellationRequested();
                    var elapsed = Time.unscaledTime - start;
                    Sample(targetGrade, elapsed, hold);
                    if (!sounded && elapsed >= impactDuration) { sounded = true; impactSound.PlayAtApp(); }
                    await Awaitable.NextFrameAsync(cancellation);
                }
            } finally { if (this) content.SetActive(false); }
        }
        public void Preview(int value, float elapsed) => Sample(value, elapsed, holdDuration);

        void Sample(int value, float elapsed, float hold) {
            content.SetActive(true);
            grade.text = PlayerGrades.Label((PlayerGrade)value);
            var incoming = Mathf.Clamp01(elapsed / impactDuration);
            var outgoing = Mathf.Clamp01((elapsed - impactDuration - hold) / exitDuration);
            group.alpha = Mathf.Clamp01(incoming * 3) * (1 - outgoing);
            band.localScale = Vector3.one * Mathf.Lerp(startScale, 1, incoming * incoming * incoming);
            var after = Mathf.Clamp01((elapsed - impactDuration) / impactDuration);
            band.anchoredPosition = new Vector2(Mathf.Sin(after * 46) * shakeAmplitude * (1 - after), -outgoing * 50);
            flash.alpha = elapsed >= impactDuration ? .6f * Mathf.Pow(1 - after, 2) : 0;
        }
    }
}
