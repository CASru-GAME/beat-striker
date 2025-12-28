using UnityEngine;

namespace Core.LargeSatan {

    public class GroundChecker : MonoBehaviour {
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