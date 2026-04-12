using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using System;
using System.Collections.Generic;
using Core.Striker.Components;

namespace Core.LargeHero {
    
    public class RushState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
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

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
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

        // このステートにいる間、毎フレーム呼ばれる
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

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
            if (activeBeamEffect) {
                Destroy(activeBeamEffect);
                activeBeamEffect = null;
            }
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
        }

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
