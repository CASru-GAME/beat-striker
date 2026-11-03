using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class BootIntroSlideshow : MonoBehaviour {
        [SerializeField] Sprite[] slides;

        GameObject overlayRoot;
        RectTransform slideRectTransform;
        Image slideImage;
        int currentIndex;
        int lastScreenWidth;
        int lastScreenHeight;
        bool completed;

        void Start() {
            Play();
        }

        void Update() {
            if (overlayRoot == null || !overlayRoot.activeSelf) {
                return;
            }

            if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height) {
                ApplyAspectFill();
            }
        }

        void OnDestroy() {
            if (overlayRoot != null) {
                Destroy(overlayRoot);
            }
        }

        public void Play() {
            currentIndex = 0;
            completed = false;

            if (slides == null || slides.Length == 0) {
                Complete();
                return;
            }

            EnsureOverlay();
            overlayRoot.SetActive(true);
            overlayRoot.transform.SetAsLastSibling();
            ShowCurrentSlide();
        }

        public void Advance() {
            if (completed) {
                return;
            }

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

        void EnsureOverlay() {
            if (overlayRoot != null) {
                return;
            }

            overlayRoot = new GameObject($"{nameof(BootIntroSlideshow)} Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = overlayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;

            var scaler = overlayRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var slideObject = new GameObject("Slide", typeof(RectTransform), typeof(Image), typeof(Button));
            slideObject.transform.SetParent(overlayRoot.transform, false);

            slideRectTransform = slideObject.GetComponent<RectTransform>();
            slideRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            slideRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            slideRectTransform.pivot = new Vector2(0.5f, 0.5f);
            slideRectTransform.anchoredPosition = Vector2.zero;

            slideImage = slideObject.GetComponent<Image>();
            slideImage.raycastTarget = true;
            slideImage.preserveAspect = true;

            var button = slideObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Advance);
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
        }
    }
}
