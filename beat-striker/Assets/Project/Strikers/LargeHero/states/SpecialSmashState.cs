using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class SpecialSmashState : StrikerState {
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] private SpecialSequenceContext specialSequenceContext;

        [SerializeField] private ParticleSystem hitEffectPrefab;
        [SerializeField] private AudioClip hitAudioClip;

        [SerializeField] private float damage = 30f;
        [SerializeField] private float hitDelay = 0.15f;

        private bool hitApplied;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip, OnAnimationEnd);
            specialSequenceContext.KeepAerialFormation(context.Rigidbody);
            hitApplied = false;

            ScheduleStateEvent(hitDelay, stateContext => {
                if (!specialSequenceContext.HasLockedVictim || hitApplied) {
                    return;
                }

                var hitPoint = specialSequenceContext.LockedVictimPosition;
                if (hitEffectPrefab) {
                    Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
                }
                if (hitAudioClip) {
                    AudioSource.PlayClipAtPoint(hitAudioClip, hitPoint);
                }
                specialSequenceContext.ApplyHitToLockedVictim(damage, Vector3.zero);
                hitApplied = true;
            });
        }

        public override void OnUpdate(IStrikerStateContext context) {
            specialSequenceContext.KeepAerialFormation(context.Rigidbody);
        }

        private void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }
    }
}
