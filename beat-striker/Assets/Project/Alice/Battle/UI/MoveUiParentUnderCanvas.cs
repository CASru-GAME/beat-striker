using UnityEngine;

namespace Alice {
    public class MoveUiParentUnderCanvas : MonoBehaviour {
        [SerializeField] Canvas targetCanvas;
        [SerializeField] bool relocateOnAwake = true;
        [SerializeField] bool resetSelfLocalPosition = false;
        [SerializeField] bool resetSelfLocalRotation = false;
        [SerializeField] bool resetSelfLocalScale = false;

        void Awake() {
            if (!relocateOnAwake) return;
            RelocateParent();
            Debug.Log($"Parent of {gameObject.name} relocated under canvas {targetCanvas?.name ?? "Auto-Detected Canvas"}");
        }

        [ContextMenu("Relocate Parent Under Canvas")]
        public void RelocateParent() {
            var uiParent = transform.parent;
            if (uiParent == null) return;

            var canvasTransform = targetCanvas != null
                ? targetCanvas.transform
                : GetComponentInParent<Canvas>(true).transform;

            var reference = FindCanvasDirectChild(uiParent, canvasTransform);
            var targetSiblingIndex = reference.GetSiblingIndex() + 1;

            uiParent.SetParent(canvasTransform, true);
            uiParent.SetSiblingIndex(Mathf.Clamp(targetSiblingIndex, 0, canvasTransform.childCount - 1));

            if (resetSelfLocalPosition) {
                transform.localPosition = Vector3.zero;
            }

            if (resetSelfLocalRotation) {
                transform.localRotation = Quaternion.identity;
            }

            if (resetSelfLocalScale) {
                transform.localScale = Vector3.one;
            }
        }

        Transform FindCanvasDirectChild(Transform start, Transform canvasTransform) {
            var current = start;
            while (current.parent != null && current.parent != canvasTransform) {
                current = current.parent;
            }

            return current.parent == canvasTransform ? current : canvasTransform;
        }
    }
}
