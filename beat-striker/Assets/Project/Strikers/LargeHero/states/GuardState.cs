using Core.Battle;
using UnityEngine;
using Core.Striker;
using System;
using R3;  

namespace Core.LargeHero {
    
    public class GuardState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerAnimationClip secondaryAnimationClip;
        [SerializeField] private bool useSecondaryAnimation;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] private Tracker tracker;
        [SerializeField] private Transform trackerTarget;
        [SerializeField] private AnimationPlayer animationPlayer;
        [SerializeField] Hurtbox shield;
        [SerializeField] GameObject sword;
        [SerializeField] bool lockHorizontalMovement = true;
        [SerializeField] float guardHitKnockbackScale = 0f;
        IDisposable disposable;
        private Tracker.TargetHandle targetHandle;
        float lockedPositionX;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            Debug.Log("GuardStateに遷移");
            if (lockHorizontalMovement) {
                lockedPositionX = context.Rigidbody.position.x;
                var velocity = context.Rigidbody.linearVelocity;
                velocity.x = 0f;
                context.Rigidbody.linearVelocity = velocity;
            }
            // キャラクターの剣を隠す
            sword.SetActive(false);
            // アニメーションの再生を開始する
            var clip = useSecondaryAnimation ? secondaryAnimationClip : animationClip;
            context.PlayAnimation(clip, context => {context.TryTransition(nextNode);
            });
            animationPlayer.PlayAnimation(secondaryAnimationClip);
            targetHandle = tracker.AddTarget(trackerTarget);
            disposable = shield.OnHit.Subscribe(hit => {
                var knockbackVelocity = guardHitKnockbackScale * hit.KnockbackVelocity;
                if (lockHorizontalMovement) {
                    knockbackVelocity.x = 0f;
                }
                context.Rigidbody.linearVelocity = knockbackVelocity;
            });
            shield.gameObject.SetActive(true);
        
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            if (!lockHorizontalMovement) {
                return;
            }

            var position = context.Rigidbody.position;
            position.x = lockedPositionX;
            context.Rigidbody.MovePosition(position);

            var velocity = context.Rigidbody.linearVelocity;
            velocity.x = 0f;
            context.Rigidbody.linearVelocity = velocity;
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
            shield.gameObject.SetActive(false);
            // 剣を再度表示する
            sword.SetActive(true);
            tracker.RemoveTarget(targetHandle); 
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
            context.Rigidbody.linearVelocity = status.KnockbackVelocity;
            context.ApplyDamage(status.Damage);
        }

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
