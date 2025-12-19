using Core.Battle;
using UnityEngine;

namespace Core.Striker
{
    [AddComponentMenu("Striker/States/Charge State")]
    public class StrikerChargeState : StrikerState
    {
        [SerializeField] private AnimationClip animationClip;
        private Components.StrikerCharger charger;

        public override void Setup(IStrikerHub hub, Rigidbody rb, Animator anim)
        {
            base.Setup(hub, rb, anim);
            charger = GetComponent<Components.StrikerCharger>();
        }

        public override void Enter()
        {
            if (animationClip != null) hub.PlayAnimation(animationClip);
            base.Enter();
            if(charger != null) charger.Charge();
        }

        public override void Exit()
        {
            base.Exit();
            if(charger != null) charger.ChargeEnd();
        }
    }
}
