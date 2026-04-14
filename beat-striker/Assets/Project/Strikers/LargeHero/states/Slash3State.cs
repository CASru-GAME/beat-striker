using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using System;
using System.Collections.Generic;
using Core.Striker.Components;


namespace Core.LargeHero {
    
    /// <summary>
    /// </summary>
    public class Slash3State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] StrikerNode comboNode;   // → 斬撃2
        [SerializeField] HitBox hitBox;
        IDisposable disposable;

        [SerializeField] ParticleSystem particleprefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] AudioClip missAudioClip;
        

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 3;
        [SerializeField] ParticleSystem SlashEffect;

        readonly List<Hit> hitsInFrame = new ();
        bool hitInState;
        bool comboRequested;
        public record Hit(Vector3 hitpoint, Hurtbox hurtbox);

        public override void OnEnter(IStrikerContext context) {
            comboRequested = false;

            context.PlayAnimation(animationClip, OnAnimationEnd);
            SlashEffect.Play();
            var swingAudioClip = missAudioClip ? missAudioClip : audioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);
            disposable = hitBox.OnEnterTrigger.Subscribe(collider => {
                if (!collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                    hurtbox = collider.GetComponentInParent<Hurtbox>();
                    if (!hurtbox) {
                        return;
                    }
                }

                if (hurtbox.transform.root == context.Rigidbody.transform.root) {
                    return;
                }

                var hitpoint = collider.ClosestPoint(hitBox.transform.position);
                hitsInFrame.Add(new (hitpoint, hurtbox));
            });
            hitsInFrame.Clear();
            hitInState = false;
        }

        void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if (!hitInState && hitsInFrame.Count >= 1) {
                var closestHit = hitsInFrame.MinBy(e => Vector3.Distance(e.hitpoint, hitBox.transform.position));

                Instantiate(particleprefab, closestHit.hitpoint, Quaternion.identity);
                AudioSource.PlayClipAtPoint(audioClip, closestHit.hitpoint);

                var nockBackDirection = Mathf.Sign(closestHit.hitpoint.x - context.Rigidbody.transform.position.x) * Vector2.right;
                closestHit.hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));

                hitsInFrame.Clear();
                hitInState = true;
            }
        }

        public override void OnExit(IStrikerContext context) {
            
            disposable.Dispose();
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
                context.TryTransition(comboNode);
        }

    }
}
