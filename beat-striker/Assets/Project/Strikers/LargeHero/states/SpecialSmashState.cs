using Core.Battle;
using UnityEngine;
using Core.Striker;
using UnityEngine.Serialization;

namespace Core.LargeHero {

    public class SpecialSmashState : StrikerState {
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] private SpecialSequenceContext specialSequenceContext;
        [SerializeField] private ParticleSystem slashEffect;

        [FormerlySerializedAs("hitEffectPrefab")]
        [SerializeField] private ParticleSystem slashEffectPrefab;
        [SerializeField] private AudioClip hitAudioClip;
        [SerializeField] private AudioClip missAudioClip;

        [SerializeField] private float damage = 30f;
        [SerializeField] private float hitDelay = 0.15f;

        private bool hitApplied;

        public override void OnEnter(IStrikerContext context) {
            slashEffect.Play();
            var swingAudioClip = missAudioClip ? missAudioClip : hitAudioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);
            context.PlayAnimation(animationClip, OnAnimationEnd);
            specialSequenceContext.KeepAerialFormation(context.Rigidbody);
            hitApplied = false;

            ScheduleStateEvent(hitDelay, stateContext => {
                if (!specialSequenceContext.HasLockedVictim || hitApplied) {
                    return;
                }

                var hitPoint = specialSequenceContext.LockedVictimPosition;
                if (slashEffectPrefab) {
                    Instantiate(slashEffectPrefab, hitPoint, Quaternion.identity);
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
