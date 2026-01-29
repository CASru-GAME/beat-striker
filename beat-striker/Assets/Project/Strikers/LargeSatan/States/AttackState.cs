using Core.Battle;
using UnityEngine;
using Core.Striker;
using Unity.VisualScripting;
using R3;
using System;

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

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip,OnAnimationEnd);
            disposable = hitBox.OnEnterTrigger.Subscribe(collider => {
                if(collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                    var hitPoint = collider.ClosestPoint(hitBox.transform.position);
                    var particleInstance = Instantiate(particlePrefab, hitPoint, Quaternion.identity);
                    particleInstance.Play();

                    AudioSource.PlayClipAtPoint(audioClip,hitPoint);

                    var nockBackDirection = Mathf.Sign(hitPoint.x - context.Rigidbody.transform.position.x) * Vector2.right;

                    hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));
                }
            });

        }

        public void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }
        
        

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
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
