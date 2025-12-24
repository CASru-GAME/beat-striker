using UnityEngine;
using Core.Striker.Darling.Components;

namespace Core.Striker.Darling.Nodes {

    [AddComponentMenu(" StrikerComponents/Locomotion State Resolver", 0)]
    public class DarlingLocomotionNode : StrikerNode {
        [SerializeField] private float walkSpeedThreshold = 3f, jumpSinThreshold = 0.5f, fallSinThreshold = -0.5f;
        [SerializeField] private StrikerNode idle, walkForward, walkBackward;
        [SerializeField] private StrikerNode air, dashUp, dashDown, dashForward, dashBackward ;
        [SerializeField] private DarlingGroundCheck groundCheck;

        public override void OnTryTransition(IStrikerNodeContext hub) {
            var velocity = new Vector2(hub.Rigidbody.linearVelocity.x, hub.Rigidbody.linearVelocity.y);
            var speed = velocity.magnitude;
            var direction = velocity.normalized;
            var IsGrounded = groundCheck.IsGrounded;

            if (IsGrounded && speed < walkSpeedThreshold) {
                if(direction.x > 0) hub.TryTransition(walkForward);
                else if(direction.x < 0) hub.TryTransition(walkBackward);
                else hub.TryTransition(idle);
            }
            else {
                if(direction.y > jumpSinThreshold) hub.TryTransition(dashUp);
                else if(direction.y < fallSinThreshold) hub.TryTransition(dashDown);
                else {
                    if(direction.x > 0) hub.TryTransition(dashForward);
                    else if(direction.x < 0) hub.TryTransition(dashBackward);
                    else hub.TryTransition(air);
                }
            }
        }
    }
}
