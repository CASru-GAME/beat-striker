using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class SpecialLandState : StrikerState {
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] private SpecialSequenceContext specialSequenceContext;
        [SerializeField] private ParticleSystem slashEffect;
        [SerializeField] private float finalDamage = 20f;
        [SerializeField] private float finalKnockbackSpeedX = 12f;
        [SerializeField] private float finalKnockbackSpeedY = 4f;

        public override void OnEnter(IStrikerContext context) {
            slashEffect.Play();

            specialSequenceContext.ReleaseVictimWithFinalHit(context.Rigidbody, finalDamage, finalKnockbackSpeedX, finalKnockbackSpeedY);
            context.PlayAnimation(animationClip, OnAnimationEnd);
        }

        public override void OnExit(IStrikerContext context) {
            specialSequenceContext.ForceReleaseVictim();
        }

        private void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }
    }
}
