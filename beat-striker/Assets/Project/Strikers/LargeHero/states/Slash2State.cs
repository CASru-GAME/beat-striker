using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using Core.Striker.Components;


namespace Core.LargeHero {
    
    /// <summary>
    /// </summary>
    public class Slash2State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] StrikerNode comboNode;   // → 斬撃2
        [SerializeField] AttackPlayer attackPlayer;

        [SerializeField] ParticleSystem particleprefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] AudioClip missAudioClip;
        

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 3;
        [SerializeField] ParticleSystem SlashEffect;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip, OnAnimationEnd);
            SlashEffect.Play();
            var swingAudioClip = missAudioClip ? missAudioClip : audioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);

            attackPlayer.OnFilterHit
                .Subscribe(collider => {
                    if (!collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                        hurtbox = collider.GetComponentInParent<Hurtbox>();
                        if (!hurtbox) {
                            return false;
                        }
                    }

                    return hurtbox.transform.root != context.Rigidbody.transform.root;
                })
                .AddTo(disposables);

            attackPlayer.OnHit
                .Subscribe(collider => {
                    if (!collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                        hurtbox = collider.GetComponentInParent<Hurtbox>();
                        if (!hurtbox) {
                            return;
                        }
                    }

                    var hitpoint = collider.ClosestPoint(attackPlayer.transform.position);
                    Instantiate(particleprefab, hitpoint, Quaternion.identity);
                    AudioSource.PlayClipAtPoint(audioClip, hitpoint);

                    var nockBackDirection = Mathf.Sign(hitpoint.x - context.Rigidbody.transform.position.x) * Vector2.right;
                    hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));
                })
                .AddTo(disposables);

            attackPlayer.Emit();
        }

        void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        public override void OnUpdate(IStrikerStateContext context) {}

        public override void OnExit(IStrikerContext context) {
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
                context.TryTransition(comboNode);
        }

    }
}
