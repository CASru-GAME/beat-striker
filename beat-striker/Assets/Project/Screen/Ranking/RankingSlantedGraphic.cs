using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    // Hard diagonal cuts match the menu and character selection panels.
    [RequireComponent(typeof(CanvasRenderer))]
    public class RankingSlantedGraphic : MaskableGraphic {
        public float slant;
        public Color endColor = Color.white;
        public bool triangle;
        public bool pointUp;

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            var r = rectTransform.rect;
            if (triangle) {
                var tip = pointUp ? r.yMax : r.yMin;
                var baseline = pointUp ? r.yMin : r.yMax;
                vh.AddVert(new Vector3(r.xMin, baseline), color, Vector2.zero);
                vh.AddVert(new Vector3(r.center.x, tip), color, Vector2.up);
                vh.AddVert(new Vector3(r.xMax, baseline), color, Vector2.one);
                if (pointUp) vh.AddTriangle(0, 1, 2);
                else vh.AddTriangle(2, 1, 0);
                return;
            }
            var cut = Mathf.Min(Mathf.Abs(slant), r.width * 0.45f);
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin + cut, r.yMax), color, Vector2.up);
            vh.AddVert(new Vector3(r.xMax, r.yMax), endColor, Vector2.one);
            vh.AddVert(new Vector3(r.xMax - cut, r.yMin), endColor, Vector2.right);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
        }
    }

    static class RankingUi {
        public static readonly Color Ink = new Color32(8, 8, 10, 255);
        public static readonly Color Yellow = new Color32(255, 220, 0, 255);
        public static readonly Color Cyan = new Color32(0, 206, 224, 255);
        public static readonly Color Pink = new Color32(255, 63, 116, 255);

        public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = low; r.offsetMax = high;
            return r;
        }

        public static RankingSlantedGraphic Band(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high, Color color, Color end, float slant = 0) {
            var rect = Rect(parent, name, min, max, low, high);
            var graphic = rect.gameObject.AddComponent<RankingSlantedGraphic>();
            graphic.color = color; graphic.endColor = end; graphic.slant = slant; graphic.raycastTarget = false;
            return graphic;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, string value, Vector2 min, Vector2 max, Vector2 low, Vector2 high, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft) {
            var rect = Rect(parent, name, min, max, low, high);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSharedMaterial = font.material;
            text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Truncate; text.raycastTarget = false;
            return text;
        }
    }
}
