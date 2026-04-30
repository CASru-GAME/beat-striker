using UnityEngine;
using UnityEngine.EventSystems;

namespace Alice {
    public class VirtualTouchStickDragArea : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler {
        [SerializeField] VirtualTouchControllerCanvasView view;
        bool isPointerActive;

        public void OnInitializePotentialDrag(PointerEventData eventData) {
            isPointerActive = true;
            view.BeginDrag(eventData.position, eventData.pressEventCamera);
        }

        public void OnBeginDrag(PointerEventData eventData) {
            if (isPointerActive) {
                return;
            }

            isPointerActive = true;
            view.BeginDrag(eventData.position, eventData.pressEventCamera);
        }

        public void OnDrag(PointerEventData eventData) {
            view.UpdateDrag(eventData.position, eventData.pressEventCamera);
        }

        public void OnEndDrag(PointerEventData eventData) {
            EndPointerInteraction();
        }

        public void OnPointerUp(PointerEventData eventData) {
            EndPointerInteraction();
        }

        void EndPointerInteraction() {
            if (!isPointerActive) {
                return;
            }

            isPointerActive = false;
            view.EndDrag();
        }
    }
}