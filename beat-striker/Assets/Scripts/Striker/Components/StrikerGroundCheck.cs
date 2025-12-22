using UnityEngine;

namespace Core.Striker.Components
{
    [AddComponentMenu(" StrikerComponents/Ground Check", 0)]
    public class StrikerGroundCheck : MonoBehaviour
    {
        public bool IsGround { get; private set; }

        private void OnCollisionStay(Collision collision)
        {
            foreach (var contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    IsGround = true;
                    return;
                }
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            IsGround = false;
        }
    }
}
