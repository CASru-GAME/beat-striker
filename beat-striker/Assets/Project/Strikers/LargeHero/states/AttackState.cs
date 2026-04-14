using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using Core.Striker.Components;


namespace Core.LargeHero {
    
    /// <summary>
    /// </summary>
    public class AttackState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] StrikerNode comboNode;   // 次の斬撃ステート（斬撃3なら空）
        [SerializeField] AttackPlayer attackPlayer;
        [SerializeField] float moveSpeed = 3;

        [SerializeField] ParticleSystem particleprefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] AudioClip missAudioClip;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 3;

        public override void OnEnter(IStrikerContext context) {
            Debug.Log("AttackState: OnEnter");
            context.PlayAnimation(animationClip, OnAnimationEnd);
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
                    Destroy(Instantiate(particleprefab, hitpoint, Quaternion.identity), 5f);
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

        public override void OnUpdate(IStrikerStateContext context) {
            context.Rigidbody.linearVelocity = moveSpeed * context.InputDirection;
        }

        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            context.TryTransition(comboNode);
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
