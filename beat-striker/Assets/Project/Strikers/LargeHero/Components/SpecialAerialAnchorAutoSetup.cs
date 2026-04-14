using UnityEngine;

namespace Core.LargeHero {
    public class SpecialAerialAnchorAutoSetup : MonoBehaviour {
        [Tooltip("自動生成するアンカー名。指定した名前の子オブジェクトを探し、存在しなければこの名前で作成して空中基準点として利用します。")]
        [SerializeField] string anchorName = "SpecialAerialAnchor";
        [Tooltip("アンカーをローカル位置でどこに置くかの既定値。新規作成時の位置を調整し、空中センターの高さや前後を調節します。")]
        [SerializeField] Vector3 localPosition = new(0f, 2.6f, 0f);
        [Tooltip("手動で割り当てられた空中センターの参照。未設定時は自動セットアップで子オブジェクトを作成または検索して設定されます。")]
        [SerializeField] Transform aerialCenterPoint;
        [Tooltip("インスペクタで表示するギズモの色。空中基準点やラインの見やすさを調整するために使います。")]
        [SerializeField] Color gizmoColor = new(0.2f, 0.9f, 1f, 0.95f);
        [Tooltip("ギズモとして描画するアンカ球の半径。視認性や選択のしやすさを調整するための値です。")]
        [SerializeField] float gizmoSphereRadius = 0.18f;
        [Tooltip("アンカーから垂直に伸ばすラインの長さ。シーンビューで高さ関係を把握しやすくするための描画設定です。")]
        [SerializeField] float gizmoVerticalLineLength = 1.2f;
        [Tooltip("アンカー位置に描画する十字のサイズ。方向や位置の目安となるクロスの長さを調整します。")]
        [SerializeField] float gizmoCrossSize = 0.45f;

        public Transform ResolveAnchor() {
            EnsureAnchorExists();
            return aerialCenterPoint;
        }

        void Awake() {
            EnsureAnchorExists();
        }

        void Reset() {
            EnsureAnchorExists();
        }

#if UNITY_EDITOR
        void OnValidate() {
            if (Application.isPlaying) {
                return;
            }

            EnsureAnchorExists();
        }
#endif

        void EnsureAnchorExists() {
            if (aerialCenterPoint) {
                return;
            }

            var existing = transform.Find(anchorName);
            if (existing) {
                aerialCenterPoint = existing;
                return;
            }

            var anchorObject = new GameObject(anchorName);
            anchorObject.transform.SetParent(transform, false);
            anchorObject.transform.localPosition = localPosition;
            aerialCenterPoint = anchorObject.transform;
        }

        void OnDrawGizmos() {
            DrawAnchorGizmo();
        }

        void OnDrawGizmosSelected() {
            DrawAnchorGizmo();
        }

        void DrawAnchorGizmo() {
            var anchor = aerialCenterPoint;
            if (!anchor) {
                anchor = transform.Find(anchorName);
            }

            if (!anchor) {
                return;
            }

            var basePosition = transform.position;
            var anchorPosition = anchor.position;

            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(anchorPosition, gizmoSphereRadius);
            Gizmos.DrawLine(basePosition, anchorPosition);
            Gizmos.DrawLine(anchorPosition, anchorPosition + Vector3.up * gizmoVerticalLineLength);

            var right = Vector3.right * gizmoCrossSize;
            var forward = Vector3.forward * gizmoCrossSize;
            Gizmos.DrawLine(anchorPosition - right, anchorPosition + right);
            Gizmos.DrawLine(anchorPosition - forward, anchorPosition + forward);
        }
    }
}
