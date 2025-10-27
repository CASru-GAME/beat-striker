


using System.Collections.Generic;
using System.Runtime.InteropServices;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

[RequireComponent(typeof(RectTransform))]
public class CursorView : MonoBehaviour, ICursorView {
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Color[] playerColors;
    [SerializeField] private float moveSpeed = 5000f;
    [SerializeField] private float accelerationFactor = 0.3f;
    private float movingTime = 0f;
    private RectTransform rectTransform;
    private RectTransform movableAreaRectTransform;
    private GameObject lastHoveredObject;
    private PlayerId playerId;
    private ICursorPresenter presenter;

    [Inject]
    public void Construct(PlayerId playerId, ICursorPresenter presenter) {
        this.playerId = playerId;
        this.presenter = presenter;
    }

    void Awake() {
        rectTransform = GetComponent<RectTransform>();
        movableAreaRectTransform = transform.parent.GetComponent<RectTransform>();
        text.text = $"P{playerId.value + 1}";
        if (playerId.value >= 0 && playerId.value < playerColors.Length) {
            text.color = playerColors[playerId.value];
        }
    }


    private PointerEventData CreatePointerEventData() {
        PointerEventData data = new(EventSystem.current) {
            position = transform.position,
            pointerId = playerId.value
        };
        return data;
    }

    private GameObject GetHoveredBotan(PointerEventData data) {
        List<RaycastResult> hoverResults = new();
        EventSystem.current?.RaycastAll(data, hoverResults);
        return FindBotan(hoverResults);
    }

    private GameObject FindBotan(List<RaycastResult> results) {
        foreach (var result in results) {
            if (result.gameObject.GetComponent<Button>()) {
                return result.gameObject;
            }
        }
        return null;
    }

    public void OnMove(Vector2 direction) {

        movingTime += Time.deltaTime;
        rectTransform.anchoredPosition += moveSpeed * (1 - Mathf.Exp(-accelerationFactor * movingTime)) * Time.deltaTime * direction;

        rectTransform.anchoredPosition = new Vector2(
            Mathf.Clamp(rectTransform.anchoredPosition.x, -movableAreaRectTransform.rect.width / 2, movableAreaRectTransform.rect.width / 2),
            Mathf.Clamp(rectTransform.anchoredPosition.y, -movableAreaRectTransform.rect.height / 2, movableAreaRectTransform.rect.height / 2)
        );

        PointerEventData data = CreatePointerEventData();
        GameObject currentHovered = GetHoveredBotan(data);

        if (currentHovered != lastHoveredObject) {
            if (lastHoveredObject)
                ExecuteEvents.Execute(lastHoveredObject, data, ExecuteEvents.pointerExitHandler);
            if (currentHovered)
                ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerEnterHandler);
            lastHoveredObject = currentHovered;
        }
    }

    public void OnMoveEnd() {
        movingTime = 0f;
    }

    public void OnClick() {
        PointerEventData data = CreatePointerEventData();
        GameObject currentHovered = GetHoveredBotan(data);
        //lastHoveredは更新しないこと

        if (currentHovered) {
            ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerClickHandler);
        }
    }
}
