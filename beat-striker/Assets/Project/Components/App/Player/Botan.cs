using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[System.Serializable]
public class HumanPlayerEvent : UnityEvent<int> { }

[AddComponentMenu(" Button", 0)]
public class Botan : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler {
    public event Action<PlayerPointerEventData> onHover;
    public event Action<PlayerPointerEventData> onHoverExit;
    public event Action<PlayerPointerEventData> onClick;
    public UnityEvent onClickEvent;
    private int hoverCount = 0;

    public void OnPointerEnter(PointerEventData eventData) {
        ++hoverCount;
        if (hoverCount >= 2) return;
        onHover?.Invoke(new PlayerPointerEventData(eventData));
    }

    public void OnPointerExit(PointerEventData eventData) {
        --hoverCount;
        hoverCount = Mathf.Max(0, hoverCount);
        if (hoverCount >= 1) return;
        onHoverExit?.Invoke(new PlayerPointerEventData(eventData));
    }

    public void OnPointerClick(PointerEventData eventData) {
        onClickEvent?.Invoke();
        onClick?.Invoke(new PlayerPointerEventData(eventData));
    }
}

public class PlayerPointerEventData {
        public PointerEventData EventData { get; private set; }
        public Player Player { get; private set; }

        public PlayerPointerEventData(PointerEventData eventData) {
            EventData = eventData;
            if (eventData.pointerId >= 0 && eventData.pointerId < App.Instance.players.Count) {
                Player = App.Instance.players[eventData.pointerId];
            }
        }
    }