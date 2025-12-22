using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu(" StrikerStates/Charge End State")]
    public class StrikerChargeEndState : StrikerState
    {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private StrikerState idleState;
        private IStrikerHub currentHub;

        public override void Enter(IStrikerHub hub)
        {
            currentHub = hub;
            if (animationClip != null)
            {
                hub.PlayAnimation(animationClip, 0f, 1f, OnAnimationComplete);
            }
        }

        private void OnAnimationComplete()
        {
             currentHub?.ChangeState(idleState);
        }
    }
}
