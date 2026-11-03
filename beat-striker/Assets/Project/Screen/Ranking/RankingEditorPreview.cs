using UnityEngine;

namespace Alice {
    // Scene-only sample cards allow the layout to be edited without entering Play Mode.
    [ExecuteAlways, DefaultExecutionOrder(-100)]
    public class RankingEditorPreview : MonoBehaviour {
        void OnEnable() {
            if (Application.IsPlaying(gameObject)) {
                gameObject.SetActive(false);
                return;
            }
            RefreshLayout();
        }

        void OnRectTransformDimensionsChange() {
            if (!Application.IsPlaying(gameObject)) RefreshLayout();
        }

        void RefreshLayout() {
            var scroll = GetComponentInParent<PolyAndCode.UI.RecyclableScrollRect>();
            if (!scroll || !scroll.content || !scroll.PrototypeCell) return;
            var prototypeSize = scroll.PrototypeCell.sizeDelta;
            if (prototypeSize.x <= 0) return;
            var width = scroll.content.rect.width;
            var height = width * prototypeSize.y / prototypeSize.x;
            for (var i = 0; i < transform.childCount; i++) {
                var row = (RectTransform)transform.GetChild(i);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
                row.pivot = new Vector2(0.5f, 1);
                row.sizeDelta = new Vector2(width, height);
                row.anchoredPosition = new Vector2(0, -height * i);
            }
        }
    }
}
