using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using System;
using System.Collections.Generic;
using Core.Striker.Components;


namespace Core.LargeHero {
    

    public class AttackState : StrikerState {
    /// <summary>
    /// 1谿オ蛻・・譁ャ謦・せ繝・・繝医ゅ・繝ャ繝上ヶ荳翫〒譁ャ謦・/2/3縺昴ｌ縺槭ｌ縺ョ繧、繝ウ繧ケ繧ソ繝ウ繧ケ縺ィ縺励※驟咲スョ縺吶ｋ縲・
    /// comboNode 縺ォ谺。縺ョ譁ャ謦・せ繝・・繝医ｒ險ュ螳壹☆繧九→縲√ヲ繝・ヨ謌仙粥蠕後↓繧ウ繝槭Φ繝峨〒繝√ぉ繧、繝ウ縺吶ｋ縲・
    /// </summary>

        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] StrikerNode comboNode;   // 谺。縺ョ譁ャ謦・せ繝・・繝茨シ域脈謦・縺ェ繧臥ゥコ・・
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

        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            Debug.Log("AttackState: OnEnter");
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
            comboRequested = false;

            context.PlayAnimation(animationClip, OnAnimationEnd);
            var swingAudioClip = missAudioClip ? missAudioClip : audioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);
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
            if (comboRequested && comboNode) {
                context.TryTransition(comboNode);
                return;
            }
            context.TryTransition(nextNode);
        }

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∵ッ弱ヵ繝ャ繝シ繝蜻シ縺ー繧後ｋ
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

        // 莉悶・繧ケ繝・・繝医↓驕キ遘サ縺吶ｋ逶エ蜑阪↓蜻シ縺ー繧後ｋ
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
        }

        // 謾サ謦・さ繝槭Φ繝峨′謚シ縺輔ｌ縺滓凾縺ォ蜻シ縺ー繧後ｋ・医ヲ繝・ヨ謌仙粥蠕後・縺ソ蜿励¢莉倥¢・・
        public override void OnAttackRequested(IStrikerStateContext context) {
            if (hitInState && comboNode) {
                comboRequested = true;
            }
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
        }

        // 繝溘せ縺励◆譎ゅ↓蜻シ縺ー繧後ｋ
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}


