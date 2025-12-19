using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu("Striker/States/Special State")]
    public class StrikerSpecialState : StrikerState
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
