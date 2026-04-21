using UnityEngine;
using R3;
using Alice;

namespace Core.LargeSatan {



    public class AirDownAttackState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;

        [SerializeField] float startupLag = 0.1f;
        [SerializeField] float diveSpeed = 20f;
        [SerializeField] float stopDistanceToGround = 0.1f;
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] StrikerAnimationClip landAnimationClip;
        [SerializeField] float landTransitionDelay = 0.1f;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 10;

        [SerializeField] AttackPlayer attackPlayer;
        [SerializeField] float impact = 5, landimpact = 10;

        [SerializeField] EffectPlayer landEffectPlayer;

        float elapsedTime;
        float groundRayDistance;
        bool previousUseGravity;
        bool hasLanded;

        public override void OnEnter(IStrikerContext context) {

            context.PlayAnimation(animationClip);

            elapsedTime = 0f;
            hasLanded = false;
            groundRayDistance = stopDistanceToGround * 2f;
            previousUseGravity = context.Rigidbody.useGravity;
            context.Rigidbody.useGravity = false;
            context.Rigidbody.linearVelocity = Vector3.zero;

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
                .Subscribe(hit => {
                    if (!hit.Collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                        hurtbox = hit.Collider.GetComponentInParent<Hurtbox>();
                        if (!hurtbox) {
                            return AttackPlayer.HitType.Cancel;
                        }
                    }

                    var hitStatus = new HitStatus(damage, nockbackSpeed * (hit.Position - context.Rigidbody.position).normalized);
                    var hitResult = hurtbox.GiveHit(hitStatus);
                    context.GenerateImpact(new StrikerImpact(impact * Vector3.down));

                    return hitResult.status == HitResult.Status.Guarded
                        ? AttackPlayer.HitType.Blocked
                        : AttackPlayer.HitType.Normal;
                })
                .AddTo(disposables);

            attackPlayer.Emit();
        }

        public override void OnUpdate(IStrikerStateContext context) {
            elapsedTime += Time.deltaTime;

            if (elapsedTime < startupLag) {
                context.Rigidbody.linearVelocity = Vector3.zero;
                return;
            }

            if (hasLanded) {
                context.Rigidbody.linearVelocity = Vector3.zero;
                return;
            }

            Vector3 velocity = Vector3.down * diveSpeed;
            if (ShouldStopBeforeGround(context.Rigidbody.position, velocity)) {
                HandleLanded(context);
                return;
            }

            context.Rigidbody.linearVelocity = velocity;
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.useGravity = previousUseGravity;
        }

        void HandleLanded(IStrikerStateContext context) {
            if (hasLanded) {
                return;
            }

            hasLanded = true;
            context.Rigidbody.linearVelocity = Vector3.zero;
            context.PlayAnimation(landAnimationClip);
            context.GenerateImpact(new StrikerImpact(landimpact * Vector3.up));
            landEffectPlayer.Emit(landEffectPlayer.transform);
            this.ScheduleStateEvent(landTransitionDelay, nextContext => {
                nextContext.TryTransition(nextNode);
            });
        }

        bool ShouldStopBeforeGround(Vector3 rayOrigin, Vector3 velocity) {
            if (velocity.y >= 0f) {
                return false;
            }

            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore)) {
                return false;
            }

            float downMoveThisFrame = -velocity.y * Time.deltaTime;
            float stopThreshold = stopDistanceToGround + downMoveThisFrame;
            return hit.distance <= stopThreshold;
        }

    }
}


