using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;


namespace Core {
    [AddComponentMenu(" Button", 0)]
    public class Botan : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler {
        public event Action<BotanEventData> onHover;
        public event Action<BotanEventData> onHoverExit;
        public event Action<BotanEventData> onClick;
        public UnityEvent onClickEvent;
        private int hoverCount = 0;
        [Header("クリック音(指定しなくて良い)")]
        public AudioClip clickSound;

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

        public void OnPointerDown(PointerEventData eventData) {
            onClickEvent?.Invoke();
            onClick?.Invoke(new BotanEventData(eventData));
            if (clickSound != null) {
                AudioSource.PlayClipAtPoint(clickSound, Camera.main.transform.position);
            }
        }
    }

    public class BotanEventData {
        public PointerEventData EventData { get; private set; }

        public BotanEventData(PointerEventData eventData) {
            EventData = eventData;
        }
    }
}