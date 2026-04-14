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
    public class AttackState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] StrikerNode comboNode;   // 次の斬撃ステート（斬撃3なら空）
        [SerializeField] HitBox hitBox;
        IDisposable disposable;
        [SerializeField] float moveSpeed = 3;

        [SerializeField] ParticleSystem particleprefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] AudioClip missAudioClip;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 3;

        readonly List<Hit> hitsInFrame = new ();
        bool hitInState;
        bool comboRequested;
        public record Hit(Vector3 hitpoint, Hurtbox hurtbox);

        public override void OnEnter(IStrikerContext context) {
            Debug.Log("AttackState: OnEnter");
            comboRequested = false;

            context.PlayAnimation(animationClip, OnAnimationEnd);
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
            if (comboRequested && comboNode) {
                context.TryTransition(comboNode);
                return;
            }
            context.TryTransition(nextNode);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.Rigidbody.linearVelocity = moveSpeed * context.InputDirection;
            if (!hitInState && hitsInFrame.Count >= 1) {
                var closestHit = hitsInFrame.MinBy(e => Vector3.Distance(e.hitpoint, hitBox.transform.position));

                Destroy(Instantiate(particleprefab, closestHit.hitpoint, Quaternion.identity), 5f);
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

        // 攻撃コマンドが押された時に呼ばれる（ヒット成功後のみ受け付け）
        public override void OnAttackRequested(IStrikerStateContext context) {
            if (hitInState && comboNode) {
                comboRequested = true;
            }
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
