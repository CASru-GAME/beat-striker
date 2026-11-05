using UnityEngine;
using UnityEngine.EventSystems;

namespace Alice {
    public class VirtualTouchGamePadButtonEmitter : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
        [SerializeField] VirtualTouchControllerCanvasView view;
        [SerializeField] GamePadButton button;

        public void OnPointerDown(PointerEventData eventData) {
            view.EmitButtonDown(button, eventData.pointerId);
        }

        public void OnPointerUp(PointerEventData eventData) {
            view.EmitButtonUp(button, eventData.pointerId);
        }
    }
}