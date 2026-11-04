using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    // Vector keycaps and flat stick discs stay crisp at every Canvas scale.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialControlGlyph : MaskableGraphic {
        [SerializeField] bool round;
        [SerializeField] float cornerCut = 2f;
        [SerializeField] float borderWidth = 0.7f;
        [SerializeField] Color restingFill = new(0.065f, 0.08f, 0.12f, 0.96f);
        [SerializeField] Color activeFill = new(0.85f, 0.12f, 0.17f, 1f);
        [SerializeField] Color restingEdge = new(0.49f, 0.55f, 0.66f, 0.7f);
        [SerializeField] Color activeEdge = new(1f, 0.43f, 0.46f, 1f);
        float focus, press, ripple;

        public void SetMotion(float focus, float press, float ripple) {
            this.focus = focus;
            this.press = press;
            this.ripple = ripple;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var edge = Color.Lerp(restingEdge, activeEdge, focus);
            var fill = Color.Lerp(restingFill, activeFill, press);
            if (ripple > 0f && ripple < 1f) {
                var expanded = rect;
                var spread = 1f + 5f * ripple;
                expanded.xMin -= spread; expanded.xMax += spread;
                expanded.yMin -= spread; expanded.yMax += spread;
                var halo = activeEdge;
                halo.a *= (1f - ripple) * 0.45f;
                Ring(vh, expanded, 0.5f, halo);
            }
            Disc(vh, rect, edge);
            rect.xMin += borderWidth; rect.xMax -= borderWidth;
            rect.yMin += borderWidth; rect.yMax -= borderWidth;
            Disc(vh, rect, fill);
        }

        Vector2 Point(Rect rect, int index, int count) {
            if (round) {
                var angle = index * Mathf.PI * 2f / count;
                return rect.center + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), rect.size * 0.5f);
            }
            var c = Mathf.Min(cornerCut, Mathf.Min(rect.width, rect.height) * 0.25f);
            return index switch {
                0 => new(rect.xMax - c, rect.yMax), 1 => new(rect.xMin + c, rect.yMax),
                2 => new(rect.xMin, rect.yMax - c), 3 => new(rect.xMin, rect.yMin + c),
                4 => new(rect.xMin + c, rect.yMin), 5 => new(rect.xMax - c, rect.yMin),
                6 => new(rect.xMax, rect.yMin + c), _ => new(rect.xMax, rect.yMax - c),
            };
        }

        void Disc(VertexHelper vh, Rect rect, Color tint) {
            var start = vh.currentVertCount;
            var count = round ? 64 : 8;
            vh.AddVert(rect.center, tint * color, Vector2.zero);
            for (var i = 0; i < count; i++) vh.AddVert(Point(rect, i, count), tint * color, Vector2.zero);
            for (var i = 0; i < count; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
        }

        void Ring(VertexHelper vh, Rect rect, float width, Color tint) {
            var start = vh.currentVertCount;
            var count = round ? 64 : 8;
            var inner = new Rect(rect.xMin + width, rect.yMin + width, rect.width - width * 2f, rect.height - width * 2f);
            for (var i = 0; i < count; i++) {
                vh.AddVert(Point(rect, i, count), tint * color, Vector2.zero);
                vh.AddVert(Point(inner, i, count), tint * color, Vector2.zero);
            }
            for (var i = 0; i < count; i++) {
                var a = start + i * 2; var b = start + ((i + 1) % count) * 2;
                vh.AddTriangle(a, b, a + 1); vh.AddTriangle(a + 1, b, b + 1);
            }
        }
    }
}
