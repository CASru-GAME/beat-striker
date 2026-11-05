using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class CharacterIntroSlideshow : MonoBehaviour {
        [SerializeField] Sprite[] heroSlides;
        [SerializeField] Sprite[] wizardSlides;

        Sprite[] slides;
        TaskCompletionSource<bool> completion;

        [SerializeField] GameObject overlayRoot;
        [SerializeField] RectTransform slideRectTransform;
        [SerializeField] Image slideImage;
        public Transform ModalRoot => overlayRoot.transform;
        int currentIndex;
        int lastAdvanceFrame;
        int lastScreenWidth;
        int lastScreenHeight;
        bool completed;

        void Update() {
            if (overlayRoot == null || !overlayRoot.activeSelf) {
                return;
            }

            if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height) {
                ApplyAspectFill();
            }
        }

        void OnDestroy() {
            completion?.TrySetResult(false);

        }

        public Task<bool> PlayAsync(Striker striker) {
            slides = striker switch {
                Striker.Hero => heroSlides,
                Striker.Wizard => wizardSlides,
                _ => null,
            };
            completion = new TaskCompletionSource<bool>();
            currentIndex = 0;
            lastAdvanceFrame = Time.frameCount;
            completed = false;

            if (slides == null || slides.Length == 0) {
                Complete();
                return completion.Task;
            }

            overlayRoot.SetActive(true);
            overlayRoot.transform.SetAsLastSibling();
            ShowCurrentSlide();
            return completion.Task;
        }

        public void Advance() {
            if (completed || Time.frameCount == lastAdvanceFrame) {
                return;
            }

            lastAdvanceFrame = Time.frameCount;
            currentIndex++;

            if (currentIndex >= slides.Length) {
                Complete();
                return;
            }

            ShowCurrentSlide();
        }

        void ShowCurrentSlide() {
            slideImage.sprite = slides[currentIndex];
            slideImage.enabled = slideImage.sprite != null;
            ApplyAspectFill();
        }

        void ApplyAspectFill() {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            if (slideRectTransform == null || slideImage == null || slideImage.sprite == null) {
                return;
            }

            var spriteRect = slideImage.sprite.rect;
            var spriteWidth = Mathf.Max(1f, spriteRect.width);
            var spriteHeight = Mathf.Max(1f, spriteRect.height);
            var screenWidth = Mathf.Max(1f, Screen.width);
            var screenHeight = Mathf.Max(1f, Screen.height);

            var spriteAspect = spriteWidth / spriteHeight;
            var screenAspect = screenWidth / screenHeight;

            float width;
            float height;

            if (screenAspect > spriteAspect) {
                width = screenWidth;
                height = screenWidth / spriteAspect;
            } else {
                height = screenHeight;
                width = screenHeight * spriteAspect;
            }

            slideRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            slideRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            slideRectTransform.anchoredPosition = Vector2.zero;
        }

        void Complete() {
            if (completed) {
                return;
            }

            completed = true;
            if (overlayRoot != null) {
                overlayRoot.SetActive(false);
            }

            completion?.TrySetResult(true);
        }
    }
}
