using NUnit.Framework;
using UnityEngine;

namespace Core.LargeHero　{
    public class GroundChecker : MonoBehaviour {
        public bool IsGrounded { get; private set; }

        private void OnCollisionStay(Collision collision) {
            foreach(var contact in collision.contacts) {
                if(contact.normal.y > 0.5f) {
                    IsGrounded = true;
                    return;
                }
            }
        }
        private void OnCollisionExit(Collision collision) {
           IsGrounded = false;
        }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    }
}

