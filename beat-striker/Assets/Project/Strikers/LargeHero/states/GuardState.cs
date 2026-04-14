using Core.Battle;
using UnityEngine;
using Core.Striker;
using System;
using R3;  

namespace Core.LargeHero {


    public class GuardState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Guard;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
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

        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            Debug.Log("GuardState縺ォ驕キ遘サ");
            if (lockHorizontalMovement) {
                lockedPositionX = context.Rigidbody.position.x;
                var velocity = context.Rigidbody.linearVelocity;
                velocity.x = 0f;
                context.Rigidbody.linearVelocity = velocity;
            }
            // 繧ュ繝」繝ゥ繧ッ繧ソ繝シ縺ョ蜑」繧帝國縺・
            sword.SetActive(false);
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
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

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∵ッ弱ヵ繝ャ繝シ繝蜻シ縺ー繧後ｋ
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

        // 莉悶・繧ケ繝・・繝医↓驕キ遘サ縺吶ｋ逶エ蜑阪↓蜻シ縺ー繧後ｋ
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
            shield.gameObject.SetActive(false);
            // 蜑」繧貞・蠎ヲ陦ィ遉コ縺吶ｋ
            sword.SetActive(true);
            tracker.RemoveTarget(targetHandle); 
        }

        // 謾サ謦・さ繝槭Φ繝峨′謚シ縺輔ｌ縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        // 繝√Ε繝シ繧ク繧ウ繝槭Φ繝峨′謚シ縺輔ｌ縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnChargeRequested(IStrikerStateContext context) {
        }

        // 繝繝・す繝・繧ウ繝槭Φ繝峨′謚シ縺輔ｌ縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnDashRequested(IStrikerStateContext context) {
        }

        // 繧ャ繝シ繝峨さ繝槭Φ繝峨′謚シ縺輔ｌ縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        // 謾サ謦・ｒ蜿励¢縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
            context.Rigidbody.linearVelocity = status.KnockbackVelocity;
            context.ApplyDamage(status.Damage);
        }

        // 繝溘せ縺励◆譎ゅ↓蜻シ縺ー繧後ｋ
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}


