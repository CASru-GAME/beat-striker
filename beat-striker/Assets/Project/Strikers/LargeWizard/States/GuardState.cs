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

        [SerializeField] Hurtbox shield;
        [SerializeField, Min(0f)] float shieldScaleSpeed = 6f;

        IDisposable disposable;
        Vector3 defaultShieldScale;
        bool hasDefaultShieldScale;
        bool isShieldScaling;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext 
        context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip,context => {
                context.TryTransition(nextNode);
            });

            disposable = shield.OnHit.Subscribe(hit => {
                context.Rigidbody.linearVelocity = 0.5f * hit.KnockbackVelocity;
            });

            if (!hasDefaultShieldScale) {
                defaultShieldScale = shield.transform.localScale;
                hasDefaultShieldScale = true;
            }

            shield.gameObject.SetActive(true);
            shield.transform.localScale = Vector3.zero;

            if (shieldScaleSpeed <= 0f) {
                shield.transform.localScale = defaultShieldScale;
                isShieldScaling = false;
                return;
            }

            isShieldScaling = true;

            AudioSource.PlayClipAtPoint(audioClip, shield.transform.position);
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            if (!isShieldScaling) return;

            shield.transform.localScale = Vector3.MoveTowards(
                shield.transform.localScale,
                defaultShieldScale,
                shieldScaleSpeed * Time.deltaTime
            );

            if (shield.transform.localScale == defaultShieldScale) {
                isShieldScaling = false;
            }
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
            shield.transform.localScale = defaultShieldScale;
            isShieldScaling = false;
            shield.gameObject.SetActive(false);
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
