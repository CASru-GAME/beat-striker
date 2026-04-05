using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Alice {
    public interface ICursor {
        int PlayerId { get; }
        void SetDirection(Vector2 direction);
        void StopMove();
        void Click();
        void DestroyCursor();
    }

    [RequireComponent(typeof(RectTransform))]
    public class Cursor : MonoBehaviour, ICursor {
        [SerializeField] TextMeshProUGUI playerLabel;
        [SerializeField] Image spriteRenderer;
        [SerializeField] Sprite[] playerSprites;
        [SerializeField] float moveSpeedConvergenceValue = 5000f;
        [SerializeField, Range(0.01f, 0.99f)] float convergenceRatioAtTime = 0.8f;
        [SerializeField] float convergenceTimeSeconds = 1.0f;

        RectTransform rectTransform;
        RectTransform movableAreaRectTransform;
        Vector2 currentDirection = Vector2.zero;
        float movingTime;
        GameObject lastHoveredObject;

        public int PlayerId { get; private set; }

        void Awake() {
            rectTransform = GetComponent<RectTransform>();
            movableAreaRectTransform = transform.parent.GetComponent<RectTransform>();
        }

        public void Construct(int playerId) {
            PlayerId = playerId;
            playerLabel.text = $"P{playerId + 1}";

            spriteRenderer.sprite = playerSprites[playerId % playerSprites.Length];
        }

        public void SetDirection(Vector2 direction) {
            currentDirection = direction;
        }

        public void StopMove() {
            movingTime = 0f;
            currentDirection = Vector2.zero;
        }

        public void Click() {
            Debug.Log($"Player {PlayerId + 1} clicked at position {rectTransform.anchoredPosition}");
            if (!TryCreatePointerEventData(out var data)) {
                return;
            }

            var currentHovered = GetHoveredInteractable(data);
            if (currentHovered) {
                ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerClickHandler);
            }
        }

        public void DestroyCursor() {
            Destroy(gameObject);
        }

        void Update() {
            if (currentDirection == Vector2.zero) {
                return;
            }

            movingTime += Time.deltaTime;
            var normalizedRatio = Mathf.Clamp(convergenceRatioAtTime, 0.01f, 0.99f);
            var normalizedTime = Mathf.Max(convergenceTimeSeconds, 0.0001f);
            var accelerationFactor = -Mathf.Log(1f - normalizedRatio) / normalizedTime;

            rectTransform.anchoredPosition +=
                moveSpeedConvergenceValue * (1 - Mathf.Exp(-accelerationFactor * movingTime)) * Time.deltaTime * currentDirection;

            rectTransform.anchoredPosition = new Vector2(
                Mathf.Clamp(rectTransform.anchoredPosition.x, -movableAreaRectTransform.rect.width / 2, movableAreaRectTransform.rect.width / 2),
                Mathf.Clamp(rectTransform.anchoredPosition.y, -movableAreaRectTransform.rect.height / 2, movableAreaRectTransform.rect.height / 2));

            if (!TryCreatePointerEventData(out var data)) {
                return;
            }

            var currentHovered = GetHoveredInteractable(data);

            if (currentHovered != lastHoveredObject) {
                if (lastHoveredObject) {
                    ExecuteEvents.Execute(lastHoveredObject, data, ExecuteEvents.pointerExitHandler);
                }

                if (currentHovered) {
                    ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerEnterHandler);
                }

                lastHoveredObject = currentHovered;
            }
        }

        bool TryCreatePointerEventData(out PointerEventData data) {
            var eventSystem = EventSystem.current;
            if (!eventSystem) {
                data = null;
                return false;
            }

            data = new PointerEventData(eventSystem) {
                position = transform.position,
                pointerId = PlayerId,
            };

            return true;
        }

        GameObject GetHoveredInteractable(PointerEventData data) {
            var eventSystem = EventSystem.current;
            if (!eventSystem) {
                return null;
            }

            var hoverResults = new List<RaycastResult>();
            eventSystem.RaycastAll(data, hoverResults);

            foreach (var result in hoverResults) {
                var gameObject = result.gameObject;

                var pointerDownTarget = ExecuteEvents.GetEventHandler<IPointerDownHandler>(gameObject);
                if (pointerDownTarget) {
                    return pointerDownTarget;
                }

                var pointerClickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(gameObject);
                if (pointerClickTarget) {
                    return pointerClickTarget;
                }

                var pointerEnterTarget = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(gameObject);
                if (pointerEnterTarget) {
                    return pointerEnterTarget;
                }

                if (gameObject.GetComponent<Selectable>()) {
                    return gameObject;
                }
            }

            return null;
        }
    }
}