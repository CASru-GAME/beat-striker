using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu(" StrikerStates/Attack State")]
    public class StrikerAttackState : StrikerState
    {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private StrikerState idleState;
        private IStrikerHub currentHub;

        public override void Enter(IStrikerHub hub)
        {
            currentHub = hub;
            if (animationClip != null)
            {
                hub.PlayAnimation(animationClip, onComplete: OnAnimationComplete);
            }
        }

        private void OnAnimationComplete()
        {
             currentHub?.ChangeState(idleState);
        }
    }
}
