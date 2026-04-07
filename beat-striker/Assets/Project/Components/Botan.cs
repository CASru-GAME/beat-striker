using System;
using Alice;
using UnityEngine;
using UnityEngine.Events;
using R3;
using UnityEngine.EventSystems;


namespace Core {
    [AddComponentMenu(" Button", 0)]
    public class Botan : ActionEmitter,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler {
        private readonly Subject<BotanEventData> hoverSubject = new();
        private readonly Subject<BotanEventData> hoverExitSubject = new();
        private readonly Subject<BotanEventData> clickSubject = new();
        public UnityEvent onClickEvent;
        private int hoverCount = 0;
        [Header("クリック音(指定しなくて良い)")]
        public AudioClip clickSound;
        private readonly Subject<BotanEventData> exitSubject = new();
        public override Observable<BotanEventData> OnClickEvent => clickSubject;
        public override Observable<BotanEventData> OnHoverEvent => hoverSubject;
        public override Observable<BotanEventData> OnHoverExitEvent => hoverExitSubject;

        public void OnEnable() {
            hoverCount = 0;
        }

        public void OnPointerEnter(PointerEventData eventData) {
            ++hoverCount;
            if (hoverCount >= 2) return;
            hoverSubject.OnNext(new BotanEventData(eventData));
        }

        public void OnPointerExit(PointerEventData eventData) {
            --hoverCount;
            hoverCount = Mathf.Max(0, hoverCount);
            if (hoverCount >= 1) return;
            hoverExitSubject.OnNext(new BotanEventData(eventData));
        }

        public void OnPointerDown(PointerEventData eventData) {
            onClickEvent?.Invoke();
            clickSubject.OnNext(new BotanEventData(eventData));
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