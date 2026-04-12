using Core.Battle;
using UnityEngine;
using Core.Striker;
using Core.Striker.Components;
using UnityEngine.Serialization;

namespace Core.LargeHero {


    public class SpecialLaunchState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Special;
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] private StrikerNode noHitFallbackNode;
        [SerializeField] private SpecialSequenceContext specialSequenceContext;
        [SerializeField] private ParticleSystem slashEffect;

        [FormerlySerializedAs("hitEffectPrefab")]
        [SerializeField] private ParticleSystem slashEffectPrefab;
        [SerializeField] private AudioClip hitAudioClip;
        [SerializeField] private AudioClip missAudioClip;

        [SerializeField] private float damage = 20f;
        [SerializeField] private Vector3 launchHitBoxOffset = new(1.35f, 1.0f, 0f);
        [SerializeField] private Vector3 launchHitBoxHalfExtents = new(1.15f, 1.0f, 1.2f);
        [SerializeField] private LayerMask victimLayerMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        private readonly Collider[] overlapResults = new Collider[32];
        private bool hitInState;

        public record Hit(Vector3 hitPoint, Hurtbox hurtbox);

        public override void OnEnter(IStrikerContext context) {
            specialSequenceContext.ForceReleaseVictim();
            slashEffect.Play();
            var swingAudioClip = missAudioClip ? missAudioClip : hitAudioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);
            context.PlayAnimation(animationClip, OnAnimationEnd);
            hitInState = false;
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if (!hitInState) {
                if (TryGetLaunchHit(context, out var hit)) {
                    specialSequenceContext.LockVictim(hit.hurtbox, context.Rigidbody);
                    specialSequenceContext.ApplyHitToLockedVictim(damage, Vector3.zero);
                    if (slashEffectPrefab) {
                        Instantiate(slashEffectPrefab, hit.hitPoint, Quaternion.identity);
                    }
                    if (hitAudioClip) {
                        AudioSource.PlayClipAtPoint(hitAudioClip, hit.hitPoint);
                    }

                    hitInState = specialSequenceContext.HasLockedVictim;
                }

                return;
            }

            specialSequenceContext.KeepAerialFormation(context.Rigidbody);
        }

        public override void OnExit(IStrikerContext context) {}

        private void OnAnimationEnd(IStrikerStateContext context) {
            if (hitInState && specialSequenceContext.HasLockedVictim) {
                context.TryTransition(nextNode);
                return;
            }

            context.TryTransition(noHitFallbackNode);
        }

        private bool TryGetLaunchHit(IStrikerStateContext context, out Hit hit) {
            var selfTransform = context.Rigidbody.transform;
            var lookDirectionX = Mathf.Sign(selfTransform.forward.x);
            if (lookDirectionX == 0f) {
                lookDirectionX = 1f;
            }

            var hitBoxCenter = selfTransform.position + new Vector3(
                launchHitBoxOffset.x * lookDirectionX,
                launchHitBoxOffset.y,
                launchHitBoxOffset.z
            );

            var overlapCount = Physics.OverlapBoxNonAlloc(
                hitBoxCenter,
                launchHitBoxHalfExtents,
                overlapResults,
                Quaternion.identity,
                victimLayerMask,
                triggerInteraction
            );

            var minDistance = float.MaxValue;
            Hit closestHit = default;

            for (var i = 0; i < overlapCount; i++) {
                var collider = overlapResults[i];
                if (!collider) {
                    continue;
                }

                if (!collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                    hurtbox = collider.GetComponentInParent<Hurtbox>();
                    if (!hurtbox) {
                        continue;
                    }
                }

                if (hurtbox.transform.root == selfTransform.root) {
                    continue;
                }

                var hurtboxRigidbody = hurtbox.GetComponentInParent<Rigidbody>();
                if (hurtboxRigidbody == context.Rigidbody) {
                    continue;
                }

                var hitPoint = collider.ClosestPoint(hitBoxCenter);
                var distance = Vector3.Distance(hitPoint, selfTransform.position);
                if (distance < minDistance) {
                    minDistance = distance;
                    closestHit = new(hitPoint, hurtbox);
                }
            }

            if (minDistance == float.MaxValue) {
                hit = default;
                return false;
            }

            hit = closestHit;
            return true;
        }
    }
}


