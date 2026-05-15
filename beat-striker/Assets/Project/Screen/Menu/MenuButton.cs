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
        [SerializeField] private float scaleAnimationDuration = 0.2f, scaleAnimationAmount = 1.1f;
        private float originalAlpha;
        private Vector3 originalIconScale;

        public void Awake() {
            originalAlpha = image.color.a;
            originalIconScale = icon.localScale;
            botan = GetComponent<Botan>();
            botan.OnHoverEvent.Subscribe(data => {
                hoverSound.PlayAtApp();
                var col = image.color;
                col.a = 0f;
                image.color = col;
                LeanTween.cancel(icon);
                LeanTween.scale(icon, originalIconScale, 0).setEaseOutBack();
                LeanTween.scale(icon, icon.localScale * scaleAnimationAmount, scaleAnimationDuration).setEaseOutBack();
            });

            botan.OnHoverExitEvent.Subscribe(data => {
                var col = image.color;
                col.a = originalAlpha;
                image.color = col;
                LeanTween.cancel(icon);
                LeanTween.scale(icon, originalIconScale, scaleAnimationDuration).setEaseOutBack();
            });
        }
    }
}