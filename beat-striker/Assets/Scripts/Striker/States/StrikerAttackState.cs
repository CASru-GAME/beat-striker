using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu("Striker/States/Attack State")]
    public class StrikerAttackState : StrikerState
    {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private StrikerState idleState;

        public override void Enter()
        {
            if (animationClip != null)
            {
                hub.PlayAnimation(animationClip, OnAnimationComplete);
            }
        }

        private void OnAnimationComplete()
        {
             hub.ChangeState(idleState);
        }
    }
}
