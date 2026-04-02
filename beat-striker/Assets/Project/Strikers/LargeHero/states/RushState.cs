using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using System;
using System.Collections.Generic;
using Core.Striker.Components;
using UnityEngine.Serialization;

namespace Core.LargeHero {
    
    public class RushState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] HitBox hitBox;
        IDisposable disposable;

    [FormerlySerializedAs("particleprefab")]
    [FormerlySerializedAs("RushHitEffect")]
    [SerializeField] ParticleSystem hitEffectPrefab;
        [SerializeField] AudioClip audioClip;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 10;
        [SerializeField] float rushSpeed = 10f;
        
        

        readonly List<Hit> hitsInFrame = new ();
        bool hitInState;
        public record Hit(Vector3 hitpoint, Hurtbox hurtbox);

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);

            var direction = Mathf.Sign(context.InputDirection.x);
            var v = context.Rigidbody.linearVelocity;
            v.x = direction * rushSpeed;
            context.Rigidbody.linearVelocity = v;

            disposable = hitBox.OnEnterTrigger.Subscribe(collider => {
                if (collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                    var hitpoint = collider.ClosestPoint(hitBox.transform.position);
                    hitsInFrame.Add(new (hitpoint, hurtbox));
                }
            });
            hitsInFrame.Clear();
            hitInState = false;
        }

        void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            if (!hitInState && hitsInFrame.Count >= 1) {
                var closestHit = hitsInFrame.MinBy(e => Vector3.Distance(e.hitpoint, hitBox.transform.position));

                var hitEffect = Instantiate(hitEffectPrefab, closestHit.hitpoint, Quaternion.identity);
                hitEffect.Play();
                AudioSource.PlayClipAtPoint(audioClip, closestHit.hitpoint);

                var nockBackDirection = Mathf.Sign(closestHit.hitpoint.x - context.Rigidbody.transform.position.x) * Vector2.right;
                closestHit.hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));

                hitsInFrame.Clear();
                hitInState = true;
            }
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        // チャージコマンドが押された時に呼ばれる
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

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
