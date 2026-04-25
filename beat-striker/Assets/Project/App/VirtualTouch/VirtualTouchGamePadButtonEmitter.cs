using UnityEngine;
using UnityEngine.EventSystems;

namespace Alice {
    public class VirtualTouchGamePadButtonEmitter : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
        [SerializeField] VirtualTouchControllerCanvasView view;
        [SerializeField] GamePadButton button;

        public void OnPointerDown(PointerEventData eventData) {
            view.EmitButtonDown(button);
        }

        public void OnPointerUp(PointerEventData eventData) {
            view.EmitButtonUp(button);
        }
    }
}