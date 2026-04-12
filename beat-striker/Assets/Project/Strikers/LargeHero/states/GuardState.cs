using Core.Battle;
using UnityEngine;
using Core.Striker;
using System;
using R3;  

namespace Core.LargeHero {


    public class GuardState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Guard;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
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

        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            Debug.Log("GuardState縺ｫ驕ｷ遘ｻ");
            if (lockHorizontalMovement) {
                lockedPositionX = context.Rigidbody.position.x;
                var velocity = context.Rigidbody.linearVelocity;
                velocity.x = 0f;
                context.Rigidbody.linearVelocity = velocity;
            }
            // 繧ｭ繝｣繝ｩ繧ｯ繧ｿ繝ｼ縺ｮ蜑｣繧帝國縺・
            sword.SetActive(false);
            // 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ蜀咲函繧帝幕蟋九☆繧・
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

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∵ｯ弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｰ繧後ｋ
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

        // 莉悶・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺吶ｋ逶ｴ蜑阪↓蜻ｼ縺ｰ繧後ｋ
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
            shield.gameObject.SetActive(false);
            // 蜑｣繧貞・蠎ｦ陦ｨ遉ｺ縺吶ｋ
            sword.SetActive(true);
            tracker.RemoveTarget(targetHandle); 
        }

        // 謾ｻ謦・さ繝槭Φ繝峨′謚ｼ縺輔ｌ縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        // 繝√Ε繝ｼ繧ｸ繧ｳ繝槭Φ繝峨′謚ｼ縺輔ｌ縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnChargeRequested(IStrikerStateContext context) {
        }

        // 繝繝・す繝･繧ｳ繝槭Φ繝峨′謚ｼ縺輔ｌ縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnDashRequested(IStrikerStateContext context) {
        }

        // 繧ｬ繝ｼ繝峨さ繝槭Φ繝峨′謚ｼ縺輔ｌ縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        // 謾ｻ謦・ｒ蜿励￠縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
            context.Rigidbody.linearVelocity = status.KnockbackVelocity;
            context.ApplyDamage(status.Damage);
        }

        // 繝溘せ縺励◆譎ゅ↓蜻ｼ縺ｰ繧後ｋ
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}


