using UnityEngine;
using UnityEngine.EventSystems;

namespace Alice {
    public class ModalInputBlocker : MonoBehaviour, IPointerDownHandler, IPointerClickHandler {
        public void OnPointerDown(PointerEventData eventData) { }
        public void OnPointerClick(PointerEventData eventData) { }
    }
}
