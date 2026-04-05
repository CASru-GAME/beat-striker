using System.Collections.Generic;
using System.Text;
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
        const string LOG_PREFIX = "[Cursor]";

        [SerializeField] TextMeshProUGUI playerLabel;
        [SerializeField] Image spriteRenderer;
        [SerializeField] Sprite[] playerSprites;
        [SerializeField] float moveSpeedConvergenceValue = 5000f;
        [SerializeField, Range(0.01f, 0.99f)] float convergenceRatioAtTime = 0.8f;
        [SerializeField] float convergenceTimeSeconds = 1.0f;
        [SerializeField] bool enableBuildDebugLog = true;

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
            if (!TryCreatePointerEventData(out var data)) {
                return;
            }

            var currentHovered = GetHoveredInteractable(data);
            if (currentHovered) {
                DebugBuildLog($"{LOG_PREFIX} Click target={currentHovered.name}, playerId={PlayerId}");
                ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerClickHandler);
                return;
            }

            DebugBuildLog($"{LOG_PREFIX} Click target not found. playerId={PlayerId}");
        }

        public void DestroyCursor() {
            Destroy(gameObject);
        }

        void Update() {
            if (currentDirection != Vector2.zero) {
                movingTime += Time.deltaTime;
                var normalizedRatio = Mathf.Clamp(convergenceRatioAtTime, 0.01f, 0.99f);
                var normalizedTime = Mathf.Max(convergenceTimeSeconds, 0.0001f);
                var accelerationFactor = -Mathf.Log(1f - normalizedRatio) / normalizedTime;

                rectTransform.anchoredPosition +=
                    moveSpeedConvergenceValue * (1 - Mathf.Exp(-accelerationFactor * movingTime)) * Time.deltaTime * currentDirection;

                rectTransform.anchoredPosition = new Vector2(
                    Mathf.Clamp(rectTransform.anchoredPosition.x, -movableAreaRectTransform.rect.width / 2, movableAreaRectTransform.rect.width / 2),
                    Mathf.Clamp(rectTransform.anchoredPosition.y, -movableAreaRectTransform.rect.height / 2, movableAreaRectTransform.rect.height / 2));
            } else {
                movingTime = 0f;
            }

            if (!TryCreatePointerEventData(out var data)) {
                return;
            }

            var currentHovered = GetHoveredInteractable(data, out var hoverCandidatesSummary);

            if (currentHovered != lastHoveredObject) {
                if (lastHoveredObject) {
                    ExecuteEvents.Execute(lastHoveredObject, data, ExecuteEvents.pointerExitHandler);
                }

                if (currentHovered) {
                    ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerEnterHandler);
                }

                DebugBuildLog($"{LOG_PREFIX} Hover changed playerId={PlayerId}, from={GetObjectName(lastHoveredObject)}, to={GetObjectName(currentHovered)}\n{hoverCandidatesSummary}");

                lastHoveredObject = currentHovered;
            }
        }

        bool TryCreatePointerEventData(out PointerEventData data) {
            var eventSystem = EventSystem.current;
            if (!eventSystem) {
                DebugBuildLog($"{LOG_PREFIX} EventSystem.current is null. playerId={PlayerId}");
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
            return GetHoveredInteractable(data, out _);
        }

        GameObject GetHoveredInteractable(PointerEventData data, out string hoverCandidatesSummary) {
            var eventSystem = EventSystem.current;
            if (!eventSystem) {
                hoverCandidatesSummary = $"{LOG_PREFIX} Raycast skipped: EventSystem.current is null.";
                return null;
            }

            var hoverResults = new List<RaycastResult>();
            eventSystem.RaycastAll(data, hoverResults);
            hoverCandidatesSummary = BuildRaycastSummary(hoverResults);

            foreach (var result in hoverResults) {
                var gameObject = result.gameObject;

                var pointerDownTarget = GetActiveEventHandler<IPointerDownHandler>(gameObject);
                if (pointerDownTarget) {
                    return pointerDownTarget;
                }

                var pointerClickTarget = GetActiveEventHandler<IPointerClickHandler>(gameObject);
                if (pointerClickTarget) {
                    return pointerClickTarget;
                }

                var pointerEnterTarget = GetActiveEventHandler<IPointerEnterHandler>(gameObject);
                if (pointerEnterTarget) {
                    return pointerEnterTarget;
                }

                var selectable = gameObject.GetComponent<Selectable>();
                if (selectable && selectable.isActiveAndEnabled && selectable.interactable) {
                    return selectable.gameObject;
                }
            }

            return null;
        }

        string BuildRaycastSummary(List<RaycastResult> hoverResults) {
            if (!enableBuildDebugLog) {
                return string.Empty;
            }

            if (hoverResults.Count == 0) {
                return $"{LOG_PREFIX} Raycast result=0";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"{LOG_PREFIX} Raycast result={hoverResults.Count}");
            var max = Mathf.Min(hoverResults.Count, 6);
            for (var i = 0; i < max; i++) {
                var hit = hoverResults[i];
                builder.AppendLine($"  [{i}] go={hit.gameObject.name}, module={hit.module}, dist={hit.distance:0.###}, sortingLayer={hit.sortingLayer}, sortingOrder={hit.sortingOrder}, depth={hit.depth}");
            }

            return builder.ToString();
        }

        string GetObjectName(GameObject gameObject) {
            return gameObject ? gameObject.name : "<null>";
        }

        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void DebugBuildLog(string message) {
            if (!enableBuildDebugLog) {
                return;
            }

            Debug.Log(message, this);
        }

        static GameObject GetActiveEventHandler<T>(GameObject root) where T : IEventSystemHandler {
            var current = root.transform;
            while (current != null) {
                var components = current.GetComponents<Component>();
                for (var i = 0; i < components.Length; i++) {
                    if (components[i] is not T handler) {
                        continue;
                    }

                    if (handler is Behaviour behaviour && !behaviour.isActiveAndEnabled) {
                        continue;
                    }

                    return current.gameObject;
                }

                current = current.parent;
            }

            return null;
        }
    }
}