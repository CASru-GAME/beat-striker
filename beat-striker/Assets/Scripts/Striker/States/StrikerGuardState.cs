using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu("Striker/States/Guard State")]
    public class StrikerGuardState : StrikerState
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
