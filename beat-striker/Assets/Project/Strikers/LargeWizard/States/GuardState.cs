using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using System;

namespace Core.LargeWizard {
    
    public class GuardState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] AudioClip audioClip;          // ガードが発動したときの音
        [SerializeField] AudioClip audioClip2;         // 既存シールドを射出したときの音
        [SerializeField] float launchedShieldSpeed = 14f;
        [SerializeField] float launchedShieldDamage = 3f;
        [SerializeField] float launchedShieldKnockbackSpeed = 20f;
        [SerializeField] float launchedShieldLifetime = 3f;
        [SerializeField] Transform shieldSpawnTransform;

        [SerializeField] Hurtbox shieldPrefab;

        IDisposable disposable;
        Hurtbox shieldInstance;
        bool hasShieldBeenHit;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {

            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);

            hasShieldBeenHit = false;
            var ownerStrikerHub = context.Rigidbody.GetComponent<StrikerHub>();

            var existingGuards = context.Rigidbody.GetComponentsInChildren<Guard>(true);
            if (existingGuards.Length > 0) {
                disposable = Disposable.Create(() => { });
                for (var i = 0; i < existingGuards.Length; i++) {
                    var existingGuard = existingGuards[i];
                    existingGuard.SetOwnerStrikerHub(ownerStrikerHub);
                    existingGuard.LaunchForward(context.Rigidbody.transform.forward, launchedShieldSpeed, launchedShieldDamage, launchedShieldKnockbackSpeed, launchedShieldLifetime);
                }

                AudioSource.PlayClipAtPoint(audioClip2, context.Rigidbody.transform.position);
                return;
            }

            shieldInstance = Instantiate(shieldPrefab, context.Rigidbody.transform);
            var guard = shieldInstance.GetComponent<Guard>();
            guard.SetOwnerStrikerHub(ownerStrikerHub);
            guard.SpawnAtPositionThenReturn(shieldSpawnTransform.position, 0.3f);

            disposable = shieldInstance.OnHit.Subscribe(hit => {
                if (hasShieldBeenHit) return;

                hasShieldBeenHit = true;
                context.Rigidbody.linearVelocity = 0.5f * hit.KnockbackVelocity;
   
            });

            AudioSource.PlayClipAtPoint(audioClip, shieldInstance.transform.position);
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
            context.Rigidbody.linearVelocity = 0.5f * status.KnockbackVelocity;
        }

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
