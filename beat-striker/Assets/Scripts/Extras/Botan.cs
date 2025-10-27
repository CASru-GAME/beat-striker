using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;


namespace Core {
    [AddComponentMenu(" Button", 0)]
    public class Botan : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler {
        public event Action<BotanEventData> onHover;
        public event Action<BotanEventData> onHoverExit;
        public event Action<BotanEventData> onClick;
        public UnityEvent onClickEvent;
        private int hoverCount = 0;

        public void OnPointerEnter(PointerEventData eventData) {
            ++hoverCount;
            if (hoverCount >= 2) return;
            onHover?.Invoke(new BotanEventData(eventData));
        }

        public void OnPointerExit(PointerEventData eventData) {
            --hoverCount;
            hoverCount = Mathf.Max(0, hoverCount);
            if (hoverCount >= 1) return;
            onHoverExit?.Invoke(new BotanEventData(eventData));
        }

        public void OnPointerClick(PointerEventData eventData) {
            onClickEvent?.Invoke();
            onClick?.Invoke(new BotanEventData(eventData));
        }
    }

    public class BotanEventData {
        public PointerEventData EventData { get; private set; }

        public BotanEventData(PointerEventData eventData) {
            EventData = eventData;
        }
    }
}