using UnityEngine;

namespace Core.Striker {
    [AddComponentMenu(" StrikerStates/Dash State")]
    public class StrikerDashState : StrikerState {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private StrikerState idleState;

        [SerializeField] private float dashSpeed = 50f;
        [SerializeField] private float duration = 0.2f;
        private float timer;
        private IStrikerHub currentHub;

        public override void Enter(StrikerStateContext context) {
            currentHub = context.Hub;
            if (animationClip != null) context.Hub.PlayAnimation(animationClip);
            
            // The initial velocity setting is moved to OnUpdate to be applied continuously.
            timer = 0f; // Timer now counts up from 0
        }

        public override void OnUpdate(StrikerStateContext context) {
            if(timer < duration) {
                 // Move forward
                 var v = context.Rigidbody.linearVelocity;
                 var forward = context.Hub.GetForwardDirection();
                 v.x = forward.x * dashSpeed;
                 v.z = forward.y * dashSpeed;
                 context.Rigidbody.linearVelocity = v;
                 
                 timer += Time.deltaTime;
            } else {
                 currentHub?.ChangeState(idleState);
            }
        }
        
        // Note: If we strictly want to use animation callback, we could set timer to anim length?
        // But for Dash, gameplay feel (speed/duration) often overrides animation length.
    }
}
