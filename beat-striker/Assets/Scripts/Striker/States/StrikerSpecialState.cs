using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu(" StrikerStates/Special State")]
    public class StrikerSpecialState : StrikerState
    {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private StrikerState idleState;
        private IStrikerHub currentHub;

        public override void Enter(IStrikerHub hub)
        {
            currentHub = hub;
            if (animationClip != null)
            {
                hub.PlayAnimation(animationClip, OnAnimationComplete);
            }
        }

        private void OnAnimationComplete()
        {
             currentHub?.ChangeState(idleState);
        }
    }
}
