using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using System;
using System.Collections.Generic;
using Core.Striker.Components;
namespace Core.LargeHero {
    

    public class Slash1State : StrikerState {
    /// <summary>
    /// 譁ｬ謦・繧ｹ繝・・繝医ゅヲ繝・ヨ謌仙粥蠕後↓繧ｳ繝槭Φ繝峨〒譁ｬ謦・縺ｸ繝√ぉ繧､繝ｳ縺吶ｋ縲・
    /// </summary>

        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] StrikerNode comboNode;   // 竊・譁ｬ謦・
        [SerializeField] HitBox hitBox;
        IDisposable disposable;

        [SerializeField] ParticleSystem particleprefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] AudioClip missAudioClip;
        [SerializeField] AudioClip guradhitsound;
        

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 3;
        [SerializeField] ParticleSystem SlashEffect;

        readonly List<Hit> hitsInFrame = new ();
        bool hitInState;
        bool comboRequested;
        public record Hit(Vector3 hitpoint, Hurtbox hurtbox);

        public override void OnEnter(IStrikerContext context) {
            comboRequested = false;
            SlashEffect.Play();
            var swingAudioClip = missAudioClip ? missAudioClip : audioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);

            context.PlayAnimation(animationClip, OnAnimationEnd);
            
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

                if (closestHit.hurtbox.IsGuarding) {
                    AudioSource.PlayClipAtPoint(guradhitsound, closestHit.hitpoint);
                }
                
                else {
                    AudioSource.PlayClipAtPoint(audioClip, closestHit.hitpoint);
                }

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
            if (hitInState && comboNode) {
                context.TryTransition(comboNode);
            }
        }

    }
}


