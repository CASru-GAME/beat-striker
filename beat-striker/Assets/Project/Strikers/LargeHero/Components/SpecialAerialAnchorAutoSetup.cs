using UnityEngine;

namespace Core.LargeHero {
    public class SpecialAerialAnchorAutoSetup : MonoBehaviour {
        [SerializeField] string anchorName = "SpecialAerialAnchor";
        [SerializeField] Vector3 localPosition = new(0f, 2.6f, 0f);
        [SerializeField] Transform aerialCenterPoint;
        [SerializeField] Color gizmoColor = new(0.2f, 0.9f, 1f, 0.95f);
        [SerializeField] float gizmoSphereRadius = 0.18f;
        [SerializeField] float gizmoVerticalLineLength = 1.2f;
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
