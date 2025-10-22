using System;
using System.Collections.Generic;
using Core.EventBus;
using Core.GamePad;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;


namespace Core.App.Player {

    /// <summary>
    /// プレイヤーのカーソル
    /// ゲームパッドの入力に応じて移動し、UI要素とのインタラクションを処理する
    /// カーソルの見た目も担当する
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(RectTransform))]
    public class PlayerCursor : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private Color[] playerColors;
        [SerializeField] private float moveSpeed = 5000f;
        [SerializeField] private float accelerationFactor = 0.3f;
        private float movingTime = 0f;
        private RectTransform rectTransform;
        private RectTransform movableAreaRectTransform;
        private GameObject lastHoveredObject;
        private PlayerId playerId;
        private GamePadId gamePadId;

        void Awake() {
            rectTransform = GetComponent<RectTransform>();
            movableAreaRectTransform = transform.parent.GetComponent<RectTransform>();
            gameObject.SetActive(false);

            Bus.Subscribe<CursorActivatedMessage>(OnCursorActivated);
        }

        void OnDestroy() {
            Bus.Unsubscribe<CursorActivatedMessage>(OnCursorActivated);
        }

        void OnCursorActivated(CursorActivatedMessage msg) {
            gameObject.SetActive(true);
            playerId = msg.playerId;
            gamePadId = msg.gamePadId;

            text.text = $"P{playerId.value}";
            text.color = playerColors[playerId.value % playerColors.Length];

            Bus.Subscribe<CursorDeactivatedMessage>(OnCursorDeactivated);
            Bus.Subscribe<GamePadDirectionMessage>(OnHumanDirection);
            Bus.Subscribe<GamePadMessage>(OnHumanCommand);
        }

        void OnCursorDeactivated(CursorDeactivatedMessage msg) {
            if (msg.playerId.value != playerId.value) return;

            Bus.Unsubscribe<CursorDeactivatedMessage>(OnCursorDeactivated);
            Bus.Unsubscribe<GamePadDirectionMessage>(OnHumanDirection);
            Bus.Unsubscribe<GamePadMessage>(OnHumanCommand);

            Destroy(gameObject);
        }

        void OnHumanDirection(GamePadDirectionMessage msg) {
            if (msg.humanId.value != gamePadId.value) return;

            movingTime += Time.deltaTime;
            rectTransform.anchoredPosition += moveSpeed * (1 - Mathf.Exp(-accelerationFactor * movingTime)) * Time.deltaTime * msg.direction;

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

        void OnHumanCommand(GamePadMessage msg) {
            if (msg.humanId.value != gamePadId.value) return;

            if (msg.button == GamePadButton.Direction) {
                movingTime = 0f;
            }

            if (msg.button != GamePadButton.East) return;

            PointerEventData data = CreatePointerEventData();
            GameObject currentHovered = GetHoveredBotan(data);

            if (currentHovered) {
                ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerClickHandler);
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
                if (result.gameObject.GetComponent<Botan>() || result.gameObject.GetComponent<Button>()) {
                    return result.gameObject;
                }
            }
            return null;
        }
    }

}