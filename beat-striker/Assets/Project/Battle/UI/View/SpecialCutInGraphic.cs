using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    [RequireComponent(typeof(CanvasRenderer))]
    public class SpecialCutInGraphic : MaskableGraphic {
        [SerializeField] float cut = 36f;
        [SerializeField] Color edgeColor = new Color(0.02f, 0.03f, 0.06f, 0f);
        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            var r = rectTransform.rect;
            var bevel = Mathf.Min(cut, r.width * 0.4f);
            mesh.AddVert(new Vector3(r.xMin, r.yMin), edgeColor, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMin + bevel, r.yMax), edgeColor, Vector2.up);
            mesh.AddVert(new Vector3(r.center.x, r.yMax), color, Vector2.up);
            mesh.AddVert(new Vector3(r.center.x, r.yMin), color, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax, r.yMax), edgeColor, Vector2.one);
            mesh.AddVert(new Vector3(r.xMax - bevel, r.yMin), edgeColor, Vector2.right);
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
            mesh.AddTriangle(3, 2, 4); mesh.AddTriangle(4, 5, 3);
        }
    }
}
