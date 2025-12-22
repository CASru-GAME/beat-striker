using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu(" StrikerStates/Guard State")]
    public class StrikerGuardState : StrikerState
    {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private StrikerState idleState;
        private IStrikerHub currentHub;

        public override void Enter(StrikerStateContext context)
        {
            currentHub = context.Hub;
            if (animationClip != null)
            {
                context.Hub.PlayAnimation(animationClip, OnAnimationComplete);
            }
         }
 
         private void OnAnimationComplete()
         {
              currentHub?.ChangeState(idleState);
         }
    }
}
