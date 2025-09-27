using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[System.Serializable]
public class HumanPlayerEvent : UnityEvent<int> { }

[AddComponentMenu(" Button", 0)]
public class Botan : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler {
    [Header("Events")]
    public Action<HumanPlayer> onHover;
    public Action<HumanPlayer> onHoverExit;
    public Action<HumanPlayer> onClick;
    public UnityEvent onClickEvent;

    public void OnPointerEnter(PointerEventData eventData) {
        if (eventData.pointerId < 0 || eventData.pointerId >= App.Instance.players.Count) return;
        onHover?.Invoke(App.Instance.players[eventData.pointerId]);
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (eventData.pointerId < 0 || eventData.pointerId >= App.Instance.players.Count) return;
        onHoverExit?.Invoke(App.Instance.players[eventData.pointerId]);
    }

    public void OnPointerClick(PointerEventData eventData) {
        onClickEvent?.Invoke();
        if (eventData.pointerId < 0 || eventData.pointerId >= App.Instance.players.Count) return;
        onClick?.Invoke(App.Instance.players[eventData.pointerId]);
    }
}
