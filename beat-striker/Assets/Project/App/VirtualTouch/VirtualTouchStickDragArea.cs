using UnityEngine;
using UnityEngine.EventSystems;

namespace Alice {
    public class VirtualTouchStickDragArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        [SerializeField] VirtualTouchControllerCanvasView view;

        public void OnBeginDrag(PointerEventData eventData) {
            view.BeginDrag(eventData.position);
        }

        public void OnDrag(PointerEventData eventData) {
            view.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData) {
            view.EndDrag();
        }
    }
}