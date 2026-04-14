using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using Core.Striker.Components;


namespace Core.LargeHero {

    public class AirAttackState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] AttackPlayer attackPlayer;

        [SerializeField] ParticleSystem particleprefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] AudioClip missAudioClip;
        [SerializeField] TrailRenderer swordTrail;
        [SerializeField] ParticleSystem SlashEffect;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 10;

        bool hitInState;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip, OnAnimationEnd);
            SlashEffect.Play();
            var swingAudioClip = missAudioClip ? missAudioClip : audioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);
            swordTrail.Clear();
            swordTrail.enabled = true;

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
                    hitInState = true;
                })
                .AddTo(disposables);

            attackPlayer.Emit();
            hitInState = false;
        }

        public void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        public override void OnUpdate(IStrikerStateContext context) {}

        public override void OnExit(IStrikerContext context) {
            swordTrail.enabled = false;
            swordTrail.Clear();
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        public override void OnChargeRequested(IStrikerStateContext context) {
        }

        // ダッシュコマンドが押された時に呼ばれる
        public override void OnDashRequested(IStrikerStateContext context) {
        }

        // ガードコマンドが押された時に呼ばれる
        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        // 攻撃を受けた時に呼ばれる
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
        }

        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
