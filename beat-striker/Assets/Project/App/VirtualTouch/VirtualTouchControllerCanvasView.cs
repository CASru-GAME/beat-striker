using R3;
using UnityEngine;

namespace Alice {
    public class VirtualTouchControllerCanvasView : MonoBehaviour {
        [SerializeField] Canvas targetCanvas;
        [SerializeField] RectTransform canvasRect;
        [SerializeField] RectTransform stickRoot;
        [SerializeField] RectTransform stickHandle;
        [SerializeField, Min(1f)] float stickRadius = 140f;

        readonly Subject<Vector2> directionChanged = new();
        readonly Subject<Unit> directionCanceled = new();
        readonly Subject<GamePadButton> buttonDown = new();
        readonly Subject<GamePadButton> buttonUp = new();

        Vector2 stickRootDefaultPosition;
        bool isDragging;
        bool isVisible;

        public Observable<Vector2> OnDirectionChanged => directionChanged;
        public Observable<Unit> OnDirectionCanceled => directionCanceled;
        public Observable<GamePadButton> OnButtonDown => buttonDown;
        public Observable<GamePadButton> OnButtonUp => buttonUp;

        void Awake() {
            stickRootDefaultPosition = stickRoot.anchoredPosition;
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

        public void BeginDrag(Vector2 screenPosition, Camera eventCamera = null) {
            ScreenToStickParentWorldPoint(screenPosition, eventCamera, out var worldPoint);
            stickRoot.position = worldPoint;
            stickHandle.anchoredPosition = Vector2.zero;
            stickRoot.gameObject.SetActive(true);
            stickRoot.SetAsLastSibling();
            isDragging = true;
            directionChanged.OnNext(Vector2.zero);
        }

        public void UpdateDrag(Vector2 screenPosition, Camera eventCamera = null) {
            if (!isDragging) {
                return;
            }

            ScreenToStickLocalPoint(screenPosition, eventCamera, out var currentLocalPosition);
            var clamped = Vector2.ClampMagnitude(currentLocalPosition, stickRadius);
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

        void ScreenToStickParentWorldPoint(Vector2 screenPosition, Camera eventCamera, out Vector3 worldPoint) {
            var targetRect = stickRoot.parent as RectTransform;
            if (!targetRect) {
                targetRect = canvasRect;
            }

            var uiCamera = eventCamera ? eventCamera : targetCanvas.worldCamera;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(targetRect, screenPosition, uiCamera, out worldPoint);
        }

        void ScreenToStickLocalPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 localPoint) {
            var uiCamera = eventCamera ? eventCamera : targetCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stickRoot, screenPosition, uiCamera, out localPoint);
        }

        void ResetStick() {
            stickRoot.anchoredPosition = stickRootDefaultPosition;
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
