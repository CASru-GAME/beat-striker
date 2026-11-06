using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public record TrialResultPresentation(bool Won, int StartGrade, int TargetGrade, int Attempt) {
        public bool IsPromotion => TargetGrade >= 0;
        public string Title => IsPromotion ? Won ? "昇格成功！" : "昇格失敗..."
            : Won ? $"{Attempt}連覇成功！" : $"{Attempt}連覇ならず...";
    }

    public class TrialResultView : MonoBehaviour {
        [SerializeField] ChoiceDialogView dialog;
        [SerializeField] GameObject content;
        [SerializeField] Image badge;
        [SerializeField] Sprite[] badges;
        [SerializeField] TMP_Text gradeLabel;
        [SerializeField] TMP_Text detail;
        [SerializeField] RectTransform medallion;
        [SerializeField] CanvasGroup burst;
        [SerializeField] RectTransform burstTransform;
        [SerializeField] CanvasGroup flash;
        [SerializeField] Graphic screenFlash;
        [SerializeField] CanvasGroup stamp;
        [SerializeField] RectTransform stampTransform;
        [SerializeField] CanvasGroup actions;
        [SerializeField] Graphic[] accents;
        [SerializeField] Graphic rays;
        [SerializeField] AudioClip spinSound;
        [SerializeField] AudioClip successSound;
        [SerializeField] AudioClip failureSound;
        [SerializeField] AudioClip promotionSuccessSound;
        [SerializeField] AudioClip promotionFailureSound;
        [SerializeField] Color promotionColor = new Color32(160, 216, 226, 255);
        [SerializeField] Color failureColor = new Color32(214, 135, 145, 255);
        [SerializeField] Color streakColor = new Color32(255, 222, 105, 255);
        [SerializeField, Min(.01f)] float anticipationDuration = .32f;
        [SerializeField, Min(.01f)] float spinDuration = 1.15f;
        [SerializeField, Min(.01f)] float stampDuration = .36f;
        [SerializeField, Min(.01f)] float revealDuration = .72f;
        [SerializeField, Min(.01f)] float successRevealDuration = 1.2f;
        [SerializeField] float burstRotation = 125f;
        [SerializeField] float stampStartScale = 3.4f;
        [SerializeField] float impactShake = 11f;
        [SerializeField, Min(.01f)] float screenFlashDuration = .3f;
        TrialResultPresentation presentation;
        float startedAt;
        bool playing, impacted;
        float RevealDuration => presentation.Won ? successRevealDuration : revealDuration;

        public void Play(TrialResultPresentation value) {
            presentation = value;
            startedAt = Time.unscaledTime;
            playing = true; impacted = false;
            content.SetActive(true);
            dialog.SetPresentationLocked(true);
            Configure();
            Sample(0);
            if (value.Won) spinSound.PlayAtApp();
        }

        void Configure() {
            var color = presentation.Won ? presentation.IsPromotion ? promotionColor : streakColor : failureColor;
            dialog.SetHeadingColor(color);
            foreach (var accent in accents) accent.color = color;
            rays.color = presentation.Won && presentation.IsPromotion ? Color.white : color;
            detail.text = presentation.IsPromotion
                ? presentation.Won ? "" : "階級はそのまま　もう一度、挑もう"
                : presentation.Won ? "頂点のその先へ　連覇記録を更新！" : "連覇はここまで";
        }

        void Update() {
            if (!playing) return;
            var elapsed = Time.unscaledTime - startedAt;
            var impactAt = anticipationDuration + (presentation.Won ? spinDuration : stampDuration);
            if (!impacted && elapsed >= impactAt) {
                impacted = true;
                var sound = presentation.IsPromotion
                    ? (presentation.Won ? promotionSuccessSound : promotionFailureSound)
                    : (presentation.Won ? successSound : failureSound);
                sound.PlayAtApp();
            }
            Sample(elapsed);
            if (elapsed < impactAt + RevealDuration) return;
            playing = false;
            dialog.SetPresentationLocked(false);
        }

        // Also used by the editor preview, without input, audio or progression changes.
        public void Preview(TrialResultPresentation value, float elapsed) {
            presentation = value; playing = false;
            content.SetActive(true); Configure(); Sample(elapsed);
        }

        void Sample(float elapsed) {
            var motionDuration = presentation.Won ? spinDuration : stampDuration;
            var motion = Mathf.Clamp01((elapsed - anticipationDuration) / motionDuration);
            var after = Mathf.Clamp01((elapsed - anticipationDuration - motionDuration) / RevealDuration);
            var revealed = elapsed >= anticipationDuration + motionDuration;
            var grade = presentation.IsPromotion && (!presentation.Won || revealed) ? presentation.TargetGrade : presentation.StartGrade;
            badge.sprite = badges[grade];
            var gradeName = PlayerGrades.Label((PlayerGrade)grade);
            gradeLabel.text = presentation.IsPromotion && !presentation.Won ? $"{gradeName}ならず..." : gradeName;
            var rotation = presentation.Won && !revealed ? 720f * (1f - Mathf.Pow(1f - motion, 2f)) : 0;
            badge.rectTransform.localRotation = Quaternion.Euler(0, rotation, 0);
            badge.rectTransform.localScale = Vector3.one * (revealed ? 1f + .18f * Mathf.Sin(after * Mathf.PI) * (1f - after) : 1f);
            medallion.anchoredPosition = revealed ? new Vector2(Mathf.Sin(after * 65f) * impactShake * Mathf.Pow(1f - after, 3), 0) : Vector2.zero;
            var spread = 1f - Mathf.Pow(1f - Mathf.Clamp01(after * 3f), 3f);
            burst.alpha = revealed && presentation.Won ? 1f - Mathf.SmoothStep(0f, 1f, (after - .3f) / .7f) : 0f;
            burstTransform.localScale = Vector3.one * Mathf.Lerp(.35f, 1.16f, spread);
            burstTransform.localRotation = Quaternion.Euler(0, 0, burstRotation * (1f - (1f - after) * (1f - after)));
            flash.alpha = revealed ? .7f * Mathf.Pow(1f - Mathf.Clamp01(after * 4), 2) : 0;
            var flashProgress = Mathf.Clamp01((elapsed - anticipationDuration - motionDuration) / screenFlashDuration);
            var screenFlashAlpha = revealed && presentation.Won && presentation.IsPromotion ? Mathf.Pow(1f - flashProgress, 2) : 0f;
            screenFlash.color = new Color(1f, 1f, 1f, screenFlashAlpha);
            stamp.alpha = !presentation.Won && elapsed >= anticipationDuration ? 1f : 0f;
            stampTransform.localScale = Vector3.one * (revealed ? 1f + .12f * Mathf.Sin(after * Mathf.PI * 2) * (1 - after) : Mathf.Lerp(stampStartScale, 1, motion * motion * motion));
            stampTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-22f, 0f, motion));
            actions.alpha = Mathf.Clamp01((after - .55f) / .45f);
            actions.interactable = actions.blocksRaycasts = after >= 1;
        }

        public void HidePresentation() {
            playing = false; content.SetActive(false);
            screenFlash.color = Color.clear;
            actions.alpha = 1; actions.interactable = actions.blocksRaycasts = true;
            dialog.SetPresentationLocked(false);
            dialog.SetHeadingColor(Color.white);
        }
        void OnDisable() { playing = false; }
    }
}
