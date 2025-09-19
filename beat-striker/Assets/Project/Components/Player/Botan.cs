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
        onHover?.Invoke(App.Instance.players[eventData.pointerId]);
    }

    public void OnPointerExit(PointerEventData eventData) {
        onHoverExit?.Invoke(App.Instance.players[eventData.pointerId]);
    }

    public void OnPointerClick(PointerEventData eventData) {
        onClick?.Invoke(App.Instance.players[eventData.pointerId]);
        onClickEvent?.Invoke();
    }
}
