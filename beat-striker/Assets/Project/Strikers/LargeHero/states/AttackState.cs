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
    /// 1谿ｵ蛻・・譁ｬ謦・せ繝・・繝医ゅ・繝ｬ繝上ヶ荳翫〒譁ｬ謦・/2/3縺昴ｌ縺槭ｌ縺ｮ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺ｨ縺励※驟咲ｽｮ縺吶ｋ縲・
    /// comboNode 縺ｫ谺｡縺ｮ譁ｬ謦・せ繝・・繝医ｒ險ｭ螳壹☆繧九→縲√ヲ繝・ヨ謌仙粥蠕後↓繧ｳ繝槭Φ繝峨〒繝√ぉ繧､繝ｳ縺吶ｋ縲・
    /// </summary>

        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] StrikerNode comboNode;   // 谺｡縺ｮ譁ｬ謦・せ繝・・繝茨ｼ域脈謦・縺ｪ繧臥ｩｺ・・
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

        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            Debug.Log("AttackState: OnEnter");
            // 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ蜀咲函繧帝幕蟋九☆繧・
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

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∵ｯ弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｰ繧後ｋ
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

        // 莉悶・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺吶ｋ逶ｴ蜑阪↓蜻ｼ縺ｰ繧後ｋ
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
        }

        // 謾ｻ謦・さ繝槭Φ繝峨′謚ｼ縺輔ｌ縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ・医ヲ繝・ヨ謌仙粥蠕後・縺ｿ蜿励￠莉倥￠・・
        public override void OnAttackRequested(IStrikerStateContext context) {
            if (hitInState && comboNode) {
                comboRequested = true;
            }
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
        }

        // 繝溘せ縺励◆譎ゅ↓蜻ｼ縺ｰ繧後ｋ
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}


