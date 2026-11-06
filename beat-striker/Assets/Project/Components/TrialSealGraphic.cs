using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    [RequireComponent(typeof(CanvasRenderer))]
    public class TrialSealGraphic : MaskableGraphic {
        [SerializeField, Range(8, 96)] int segments = 64;
        [SerializeField, Range(0f, .99f)] float innerRadius = .96f;
        [SerializeField, Range(.1f, 1f)] float segmentFill = .92f;
        [SerializeField, Range(0f, 1f)] float crossWidthRatio;
        [SerializeField, Range(0f, 1f)] float outerOpacity = 1f;
        [SerializeField] bool distressed;

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            var radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            var center = rectTransform.rect.center;
            var edgeColor = color;
            edgeColor.a *= outerOpacity;
            for (var i = 0; i < segments; i++) {
                var a = i * Mathf.PI * 2 / segments;
                var b = (i + segmentFill) * Mathf.PI * 2 / segments;
                var x = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                var y = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                var segmentColor = color;
                var segmentEdgeColor = edgeColor;
                var inner = innerRadius;
                if (distressed) {
                    var wear = Mathf.PerlinNoise(i * .31f, 2.7f);
                    var opacity = Mathf.SmoothStep(.12f, 1f, wear);
                    segmentColor.a *= opacity;
                    segmentEdgeColor.a *= opacity;
                    inner = Mathf.Lerp(innerRadius, 1f, Mathf.PerlinNoise(i * .43f, 7.1f) * .7f);
                }
                var start = vh.currentVertCount;
                vh.AddVert(center + x, segmentEdgeColor, Vector2.zero);
                vh.AddVert(center + y, segmentEdgeColor, Vector2.zero);
                vh.AddVert(center + y * inner, segmentColor, Vector2.zero);
                vh.AddVert(center + x * inner, segmentColor, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
            if (crossWidthRatio > 0f) {
                // Meet the ring halfway through its thickness so the strokes and outline join.
                var strokeRadius = radius * (1f + innerRadius) * .5f;
                var halfWidth = strokeRadius * crossWidthRatio;
                AddStroke(vh, center, new Vector2(1, 1).normalized, strokeRadius, halfWidth);
                AddStroke(vh, center, new Vector2(1, -1).normalized, strokeRadius, halfWidth);
            }
        }

        void AddStroke(VertexHelper vh, Vector2 center, Vector2 axis, float radius, float halfWidth) {
            const int capSegments = 4;
            var normal = new Vector2(-axis.y, axis.x);
            var start = vh.currentVertCount;
            for (var i = 0; i <= capSegments; i++) {
                var offset = Mathf.Lerp(-halfWidth, halfWidth, (float)i / capSegments);
                var reach = Mathf.Sqrt(radius * radius - offset * offset);
                var row = center + normal * offset;
                vh.AddVert(row - axis * reach, color, Vector2.zero);
                vh.AddVert(row + axis * reach, color, Vector2.zero);
                if (i == 0) continue;
                var left = start + (i - 1) * 2;
                vh.AddTriangle(left, left + 1, left + 3);
                vh.AddTriangle(left, left + 3, left + 2);
            }
        }
    }
}
