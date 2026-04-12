using Core.Battle;
using UnityEngine;
using Core.Striker;
using UnityEngine.Serialization;

namespace Core.LargeHero {

    public class SpecialChaseState : StrikerState {
        [SerializeField] private StrikerAnimationClip chaseState1;
        [SerializeField] private StrikerAnimationClip chaseState2;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] private SpecialSequenceContext specialSequenceContext;
        [FormerlySerializedAs("slashEffect")]
        [SerializeField] private ParticleSystem chaseState1SlashEffect;
        [SerializeField] private ParticleSystem chaseState2SlashEffect;

        [FormerlySerializedAs("hitEffectPrefab")]
        [SerializeField] private ParticleSystem slashEffectPrefab;
        [SerializeField] private AudioClip hitAudioClip;
        [SerializeField] private AudioClip missAudioClip;

        [SerializeField] private float damage = 25f;
        [SerializeField] private float hitDelay = 0.2f;
        [SerializeField] private float chaseState2HitDelay = 0.2f;

        private bool chaseState1HitApplied;
        private bool chaseState2HitApplied;
        private bool startedChaseState2;

        public override void OnEnter(IStrikerContext context) {
            chaseState1SlashEffect.Play();
            var swingAudioClip = missAudioClip ? missAudioClip : hitAudioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);
            context.PlayAnimation(chaseState1, OnChaseState1End);
            specialSequenceContext.KeepAerialFormation(context.Rigidbody);
            chaseState1HitApplied = false;
            chaseState2HitApplied = false;
            startedChaseState2 = false;

            var chaseState1Duration = chaseState1.clip.length / Mathf.Max(chaseState1.speed, 0.0001f);
            ScheduleStateEvent(chaseState1Duration, BeginChaseState2);

            ScheduleStateEvent(hitDelay, stateContext => {
                if (chaseState1HitApplied) {
                    return;
                }

                chaseState1HitApplied = TryApplyHitToLockedVictim();
            });
        }

        public override void OnUpdate(IStrikerStateContext context) {
            specialSequenceContext.KeepAerialFormation(context.Rigidbody);
        }

        private void OnChaseState1End(IStrikerStateContext context) {
            BeginChaseState2(context);
        }

        private void BeginChaseState2(IStrikerStateContext context) {
            if (startedChaseState2) {
                return;
            }

            startedChaseState2 = true;
            chaseState2SlashEffect.Play();
            context.PlayAnimation(chaseState2, OnAnimationEnd);

            ScheduleStateEvent(chaseState2HitDelay, stateContext => {
                if (chaseState2HitApplied) {
                    return;
                }

                chaseState2HitApplied = TryApplyHitToLockedVictim();
            });
        }

        private void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        bool TryApplyHitToLockedVictim() {
            if (!specialSequenceContext.HasLockedVictim) {
                return false;
            }

            var hitPoint = specialSequenceContext.LockedVictimPosition;
            if (slashEffectPrefab) {
                Instantiate(slashEffectPrefab, hitPoint, Quaternion.identity);
            }
            if (hitAudioClip) {
                AudioSource.PlayClipAtPoint(hitAudioClip, hitPoint);
            }

            specialSequenceContext.ApplyHitToLockedVictim(damage, Vector3.zero);
            return true;
        }
    }
}
