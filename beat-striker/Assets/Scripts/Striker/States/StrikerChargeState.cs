using Core.Battle;
using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu(" StrikerStates/Charge State")]
    public class StrikerChargeState : StrikerState
    {
        [SerializeField] private AnimationClip animationClip;
        private Components.StrikerCharger charger;

        private void Awake()
        {
            charger = GetComponent<Components.StrikerCharger>();
        }

        public override void Enter(StrikerStateContext context)
        {
            if (animationClip != null) context.Hub.PlayAnimation(animationClip);
            if(charger != null) charger.Charge();
        }

        public override void Exit()
        {
            base.Exit();
            if(charger != null) charger.ChargeEnd();
        }
    }
}
