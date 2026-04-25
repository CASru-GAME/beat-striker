using R3;
using UnityEngine;

namespace Alice {
    public class VirtualTouchControllerCanvasView : MonoBehaviour {
        [SerializeField] Canvas targetCanvas;
        [SerializeField] RectTransform canvasRect;
        [SerializeField] RectTransform stickRoot;
        [SerializeField] RectTransform stickHandle;
        [SerializeField, Min(1f)] float stickRadius = 140f;
        [SerializeField] int topLayerSortingOrder = 5000;

        readonly Subject<Vector2> directionChanged = new();
        readonly Subject<Unit> directionCanceled = new();
        readonly Subject<GamePadButton> buttonDown = new();
        readonly Subject<GamePadButton> buttonUp = new();

        Vector2 dragStartLocalPosition;
        bool isDragging;
        bool isVisible;

        public Observable<Vector2> OnDirectionChanged => directionChanged;
        public Observable<Unit> OnDirectionCanceled => directionCanceled;
        public Observable<GamePadButton> OnButtonDown => buttonDown;
        public Observable<GamePadButton> OnButtonUp => buttonUp;

        void Awake() {
            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = topLayerSortingOrder;
            ApplyVisible();
            stickRoot.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible) {
            isVisible = visible;
            ApplyVisible();
            if (!visible) {
                ResetStick();
            }
        }

        void ApplyVisible() {
            targetCanvas.gameObject.SetActive(isVisible);
        }

        public void BeginDrag(Vector2 screenPosition) {
            ScreenToCanvasLocalPoint(screenPosition, out dragStartLocalPosition);
            stickRoot.anchoredPosition = dragStartLocalPosition;
            stickHandle.anchoredPosition = Vector2.zero;
            stickRoot.gameObject.SetActive(true);
            isDragging = true;
            directionChanged.OnNext(Vector2.zero);
        }

        public void UpdateDrag(Vector2 screenPosition) {
            if (!isDragging) {
                return;
            }

            ScreenToCanvasLocalPoint(screenPosition, out var currentLocalPosition);
            var delta = currentLocalPosition - dragStartLocalPosition;
            var clamped = Vector2.ClampMagnitude(delta, stickRadius);
            stickHandle.anchoredPosition = clamped;

            var normalizedDirection = clamped / stickRadius;
            directionChanged.OnNext(normalizedDirection);
        }

        public void EndDrag() {
            if (!isDragging) {
                return;
            }

            isDragging = false;
            directionCanceled.OnNext(Unit.Default);
            ResetStick();
        }

        public void EmitButtonDown(GamePadButton button) {
            buttonDown.OnNext(button);
        }

        public void EmitButtonUp(GamePadButton button) {
            buttonUp.OnNext(button);
        }

        void ScreenToCanvasLocalPoint(Vector2 screenPosition, out Vector2 localPoint) {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, targetCanvas.worldCamera, out localPoint);
        }

        void ResetStick() {
            stickRoot.gameObject.SetActive(false);
            stickHandle.anchoredPosition = Vector2.zero;
        }

        void OnDestroy() {
            directionChanged.Dispose();
            directionCanceled.Dispose();
            buttonDown.Dispose();
            buttonUp.Dispose();
        }
    }
}
