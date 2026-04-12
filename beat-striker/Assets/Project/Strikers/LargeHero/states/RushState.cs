using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using System;
using System.Collections.Generic;
using Core.Striker.Components;

namespace Core.LargeHero {


    public class RushState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] HitBox hitBox;
        IDisposable disposable;

        [SerializeField] ParticleSystem hitEffectPrefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] AudioClip missAudioClip;
        [SerializeField] GameObject beamPrefab;
        [SerializeField] Transform firePosition;
        [SerializeField] float beamFireTime = 0.3f;
        [SerializeField] AudioClip beamAudioClip;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 10;
        [SerializeField] float rushSpeed = 10f;
        
        

        readonly List<Hit> hitsInFrame = new ();
        bool hitInState;
        bool beamSpawnedInState;
        GameObject activeBeamEffect;
        public record Hit(Vector3 hitpoint, Hurtbox hurtbox);

        void OnValidate() {
            if (!beamAudioClip) {
                beamAudioClip = audioClip;
            }
        }

        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            // 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, OnAnimationEnd);
            var swingAudioClip = missAudioClip ? missAudioClip : audioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);

            if (activeBeamEffect) {
                Destroy(activeBeamEffect);
                activeBeamEffect = null;
            }

            var direction = Mathf.Sign(context.InputDirection.x);
            var v = context.Rigidbody.linearVelocity;
            v.x = direction * rushSpeed;
            context.Rigidbody.linearVelocity = v;

            beamSpawnedInState = false;
            ScheduleStateEvent(beamFireTime, SpawnVisualOnlyBeam);

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
            if (!beamSpawnedInState) {
                SpawnVisualOnlyBeam(context);
            }
            context.TryTransition(nextNode);
        }

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∵ｯ弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｰ繧後ｋ
        public override void OnUpdate(IStrikerStateContext context) {
            if (!hitInState && hitsInFrame.Count >= 1) {
                var closestHit = hitsInFrame.MinBy(e => Vector3.Distance(e.hitpoint, hitBox.transform.position));

                var hitEffect = Instantiate(hitEffectPrefab, closestHit.hitpoint, Quaternion.identity);
                hitEffect.Play();
                AudioSource.PlayClipAtPoint(audioClip, closestHit.hitpoint);

                var nockBackDirection = Mathf.Sign(closestHit.hitpoint.x - context.Rigidbody.transform.position.x) * Vector2.right;
                closestHit.hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));

                hitsInFrame.Clear();
                hitInState = true;
            }
        }

        void SpawnVisualOnlyBeam(IStrikerStateContext context) {
            if (beamSpawnedInState) {
                return;
            }

            if (activeBeamEffect) {
                Destroy(activeBeamEffect);
            }

            var beamInstance = Instantiate(beamPrefab, firePosition.position, context.Rigidbody.transform.rotation);
            var beamClip = beamAudioClip ? beamAudioClip : audioClip;
            if (beamClip) {
                AudioSource.PlayClipAtPoint(beamClip, firePosition.position);
            }

            var colliders = beamInstance.GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders) {
                collider.enabled = false;
            }

            var rigidbodies = beamInstance.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rigidbodies) {
                rb.isKinematic = true;
                rb.detectCollisions = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var bullets = beamInstance.GetComponentsInChildren<Bullet>(true);
            foreach (var bullet in bullets) {
                bullet.enabled = false;
            }

            activeBeamEffect = beamInstance;
            beamSpawnedInState = true;
        }

        // 莉悶・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺吶ｋ逶ｴ蜑阪↓蜻ｼ縺ｰ繧後ｋ
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
            if (activeBeamEffect) {
                Destroy(activeBeamEffect);
                activeBeamEffect = null;
            }
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


