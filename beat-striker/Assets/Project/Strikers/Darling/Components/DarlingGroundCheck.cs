using UnityEngine;

namespace Core.Striker.Darling.Components {
    [AddComponentMenu(" StrikerComponents/Ground Check", 0)]
    public class DarlingGroundCheck : MonoBehaviour {
        public bool IsGrounded { get; private set; }

        private void OnCollisionStay(Collision collision) {
            foreach (var contact in collision.contacts) {
                if (contact.normal.y > 0.5f) {
                    IsGrounded = true;
                    return;
                }
            }
        }

        private void OnCollisionExit(Collision collision) {
            IsGrounded = false;
        }
    }
}
