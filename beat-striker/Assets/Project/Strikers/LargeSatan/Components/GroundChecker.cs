using R3;
using UnityEngine;

namespace Core.LargeSatan {

    public class GroundChecker : MonoBehaviour {
        public bool IsGrounded { get; private set; }
        public ReactiveProperty<bool> IsGroundedProperty { get; } = new ReactiveProperty<bool>();

        private void OnCollisionStay(Collision collision) {
            foreach (var contact in collision.contacts) {
                if (contact.normal.y > 0.5f) {
                    IsGrounded = true;
                    break;
                }
            }
            IsGroundedProperty.Value = IsGrounded;
        }

        private void OnCollisionExit(Collision collision) {
            IsGrounded = false;
            IsGroundedProperty.Value = IsGrounded;
        }
    }

}