using Core.Battle;
using UnityEngine;
using Core.Striker;
using Unity.VisualScripting;
using R3;
using System;
using System.Collections.Generic;
using Core.Striker.Components;

namespace Core.LargeWizard {


    public class AttackState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] HitBox hitBox;
        IDisposable disposable;

        [SerializeField] ParticleSystem particlePrefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] float damage = 10;
        [SerializeField] float knockbackSpeed = 10;

        readonly List<Hit> hitsInFrame = new();
        bool hitInState;
        public record Hit(Vector3 hitPoint, Hurtbox hurtBox);

        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            // 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, OnAnimationEnd);
            disposable = hitBox.OnEnterTrigger.Subscribe(collider => {
                if (collider.TryGetComponent<Hurtbox>(out var hurtbox)) {

                    var hitpoint = collider.ClosestPoint(hitBox.transform.position);
                    hitsInFrame.Add(new(hitpoint, hurtbox));

                }
            });
            hitsInFrame.Clear();
            hitInState = false;
        }

        public void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∵ｯ弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｰ繧後ｋ
        public override void OnUpdate(IStrikerStateContext context) {
            if (!hitInState && hitsInFrame.Count >= 1) {
                var closestHit = hitsInFrame.MinBy(e => Vector3.Distance(e.hitPoint, hitBox.transform.position));


                var particleInstance = Instantiate(particlePrefab, closestHit.hitPoint, Quaternion.identity);
                particleInstance.Play();

                AudioSource.PlayClipAtPoint(audioClip, closestHit.hitPoint);

                var nockBackDirection = Mathf.Sign(closestHit.hitPoint.x - context.Rigidbody.transform.position.x) * Vector2.right;

                closestHit.hurtBox.GiveHit(new HitStatus(damage, knockbackSpeed * nockBackDirection));

                Destroy(particleInstance.gameObject, 5f);

                hitsInFrame.Clear();
                hitInState = true;
            }
        }

        // 莉悶・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺吶ｋ逶ｴ蜑阪↓蜻ｼ縺ｰ繧後ｋ
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
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
        }

        // 繝溘せ縺励◆譎ゅ↓蜻ｼ縺ｰ繧後ｋ
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}


