using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using System;

namespace Core.LargeWizard {


    public class GuardState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Guard;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] AudioClip audioClip;          // 繧ャ繝シ繝峨′逋コ蜍輔＠縺溘→縺阪・髻ウ

        [SerializeField] Hurtbox shield;
        [SerializeField, Min(0f)] float shieldScaleSpeed = 6f;

        IDisposable disposable;
        Vector3 defaultShieldScale;
        bool hasDefaultShieldScale;
        bool isShieldScaling;

        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext 
        context) {
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
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

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∵ッ弱ヵ繝ャ繝シ繝蜻シ縺ー繧後ｋ
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

        // 莉悶・繧ケ繝・・繝医↓驕キ遘サ縺吶ｋ逶エ蜑阪↓蜻シ縺ー繧後ｋ
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
            shield.transform.localScale = defaultShieldScale;
            isShieldScaling = false;
            shield.gameObject.SetActive(false);
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
            context.Rigidbody.linearVelocity = 0.5f * status.KnockbackVelocity;
        }

        // 繝溘せ縺励◆譎ゅ↓蜻シ縺ー繧後ｋ
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}


