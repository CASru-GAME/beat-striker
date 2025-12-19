using UnityEngine;

namespace Core.Striker {
    [AddComponentMenu("Striker/States/Dash State")]
    public class StrikerDashState : StrikerState {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private StrikerState idleState;

        [SerializeField] private float dashSpeed = 50f;
        [SerializeField] private float duration = 0.2f;
        private float timer;

        public override void Enter() {
            if (animationClip != null) hub.PlayAnimation(animationClip);
            base.Enter(); 
            
            // The initial velocity setting is moved to OnUpdate to be applied continuously.
            timer = 0f; // Timer now counts up from 0
        }

        public override void OnUpdate() {
            if(timer < duration) {
                 // Move forward
                 var v = rb.linearVelocity;
                 var forward = hub.GetForwardDirection();
                 v.x = forward.x * dashSpeed;
                 v.z = forward.y * dashSpeed;
                 rb.linearVelocity = v;
                 
                 timer += Time.deltaTime;
            } else {
                 hub.ChangeState(idleState);
            }
        }
        
        // Note: If we strictly want to use animation callback, we could set timer to anim length?
        // But for Dash, gameplay feel (speed/duration) often overrides animation length.
    }
}
