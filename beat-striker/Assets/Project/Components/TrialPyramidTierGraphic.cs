using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    [RequireComponent(typeof(CanvasRenderer))]
    public class TrialPyramidTierGraphic : MaskableGraphic {
        [SerializeField, Range(0, 1)] float topWidth;
        [SerializeField, Range(0, 1)] float bottomWidth = 1;
        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            var r = rectTransform.rect;
            var half = r.width * .5f;
            vh.AddVert(new Vector3(r.center.x - half * bottomWidth, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(r.center.x - half * topWidth, r.yMax), color, Vector2.up);
            vh.AddVert(new Vector3(r.center.x + half * topWidth, r.yMax), color, Vector2.one);
            vh.AddVert(new Vector3(r.center.x + half * bottomWidth, r.yMin), color, Vector2.right);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
        }
    }
}
