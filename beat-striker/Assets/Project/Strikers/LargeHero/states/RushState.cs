using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using Core.Striker.Components;

namespace Core.LargeHero {
    
    public class RushState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] AttackPlayer attackPlayer;

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
        
        

        bool hitInState;
        bool beamSpawnedInState;
        GameObject activeBeamEffect;

        void OnValidate() {
            if (!beamAudioClip) {
                beamAudioClip = audioClip;
            }
        }

        public override void OnEnter(IStrikerContext context) {
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

            attackPlayer.OnFilterHit
                .Subscribe(collider => {
                    if (!collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                        hurtbox = collider.GetComponentInParent<Hurtbox>();
                        if (!hurtbox) {
                            return false;
                        }
                    }

                    return hurtbox.transform.root != context.Rigidbody.transform.root;
                })
                .AddTo(disposables);

            attackPlayer.OnHit
                .Subscribe(collider => {
                    if (!collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                        hurtbox = collider.GetComponentInParent<Hurtbox>();
                        if (!hurtbox) {
                            return;
                        }
                    }

                    var hitpoint = collider.ClosestPoint(attackPlayer.transform.position);
                    var hitEffect = Instantiate(hitEffectPrefab, hitpoint, Quaternion.identity);
                    hitEffect.Play();
                    AudioSource.PlayClipAtPoint(audioClip, hitpoint);

                    var nockBackDirection = Mathf.Sign(hitpoint.x - context.Rigidbody.transform.position.x) * Vector2.right;
                    hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));
                    hitInState = true;
                })
                .AddTo(disposables);

            attackPlayer.Emit();
            hitInState = false;
        }

        void OnAnimationEnd(IStrikerStateContext context) {
            if (!beamSpawnedInState) {
                SpawnVisualOnlyBeam(context);
            }
            context.TryTransition(nextNode);
        }

        public override void OnUpdate(IStrikerStateContext context) {}

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

        public override void OnExit(IStrikerContext context) {
            if (activeBeamEffect) {
                Destroy(activeBeamEffect);
                activeBeamEffect = null;
            }
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

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

        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
