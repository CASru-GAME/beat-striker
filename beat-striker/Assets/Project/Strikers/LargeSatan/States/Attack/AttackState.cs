using UnityEngine;
using R3;
using System;
using System.Collections.Generic;
using Alice;

namespace Core.LargeSatan {

    public class AttackState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] HitBox hitBox;
        IDisposable disposable;

        [SerializeField] ParticleSystem particlePrefab;
        [SerializeField] AudioClip audioClip;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 10;

        [SerializeField] EffectPlayer effectPlayer;
        [SerializeField] AudioClip slashSound;

        readonly List<Hit> hitsInFrame = new();
        bool hitInState;
        public record Hit(Vector3 hitPoint, Hurtbox hurtBox);

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);

            effectPlayer.Emit(effectPlayer.transform);
            AudioSource.PlayClipAtPoint(slashSound, transform.position);

            disposable = hitBox.OnEnterTrigger.Subscribe(collider => {
                if (collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                    var hitPoint = collider.ClosestPoint(hitBox.transform.position);
                    hitsInFrame.Add(new(hitPoint, hurtbox));
                }
            });
            hitsInFrame.Clear();
            hitInState = false;
        }

        public void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            if (!hitInState && hitsInFrame.Count >= 1) {
                var closestHit = hitsInFrame.MinBy(e => Vector3.Distance(e.hitPoint, hitBox.transform.position));

                Instantiate(particlePrefab, closestHit.hitPoint, Quaternion.identity);
                AudioSource.PlayClipAtPoint(audioClip, closestHit.hitPoint);
                var nockBackDirection = Mathf.Sign(closestHit.hitPoint.x - context.Rigidbody.transform.position.x) * Vector2.right;
                closestHit.hurtBox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));

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
