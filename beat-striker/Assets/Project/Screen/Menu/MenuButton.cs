using Core;
using UnityEngine;
using R3;
using UnityEngine.UI;

namespace Alice {

    [RequireComponent(typeof(Botan))]
    public class MenuButton : MonoBehaviour {

        Botan botan;
        [SerializeField] AudioClip hoverSound;
        [SerializeField] Image image;
        [SerializeField] private RectTransform icon;
        [SerializeField] CanvasGroup description;
        [SerializeField, Min(0.01f)] float descriptionShowDuration = 0.18f;
        [SerializeField, Min(0.01f)] float descriptionHideDuration = 0.12f;
        [SerializeField, Min(0f)] float descriptionSlideDistance = 14f;
        [SerializeField] private float scaleAnimationDuration = 0.2f, scaleAnimationAmount = 1.1f;
        private float originalAlpha;
        private Vector3 originalIconScale;
        RectTransform descriptionRect;
        Vector2 descriptionPosition;

        public void Awake() {
            originalAlpha = image.color.a;
            originalIconScale = icon.localScale;
            descriptionRect = (RectTransform)description.transform;
            descriptionPosition = descriptionRect.anchoredPosition;
            SetDescriptionProgress(0f);
            botan = GetComponent<Botan>();
            botan.OnHoverEvent.Subscribe(data => {
                hoverSound.PlayAtApp();
                AnimateDescription(true);
                var col = image.color;
                col.a = 0f;
                image.color = col;
                LeanTween.cancel(icon);
                LeanTween.scale(icon, originalIconScale, 0).setEaseOutBack();
                LeanTween.scale(icon, icon.localScale * scaleAnimationAmount, scaleAnimationDuration).setEaseOutBack();
            }).AddTo(this);

            botan.OnHoverExitEvent.Subscribe(data => {
                AnimateDescription(false);
                var col = image.color;
                col.a = originalAlpha;
                image.color = col;
                LeanTween.cancel(icon);
                LeanTween.scale(icon, originalIconScale, scaleAnimationDuration).setEaseOutBack();
            }).AddTo(this);
        }

        void AnimateDescription(bool show) {
            var target = description.gameObject;
            LeanTween.cancel(target);
            if (show) target.SetActive(true);

            LeanTween.value(target, description.alpha, show ? 1f : 0f, show ? descriptionShowDuration : descriptionHideDuration)
                .setEaseOutQuad()
                .setIgnoreTimeScale(true)
                .setOnUpdate(SetDescriptionProgress)
                .setOnComplete(() => {
                    if (!show) target.SetActive(false);
                });
        }

        void SetDescriptionProgress(float progress) {
            description.alpha = progress;
            descriptionRect.anchoredPosition = descriptionPosition - Vector2.up * (descriptionSlideDistance * (1f - progress));
        }

        void OnDisable() {
            LeanTween.cancel(description.gameObject);
            SetDescriptionProgress(0f);
            description.gameObject.SetActive(false);
            var col = image.color;
            col.a = originalAlpha;
            image.color = col;
            LeanTween.cancel(icon);
            icon.localScale = originalIconScale;
        }
    }
}
