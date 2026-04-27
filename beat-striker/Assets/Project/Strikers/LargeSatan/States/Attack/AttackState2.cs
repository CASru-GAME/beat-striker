using UnityEngine;
using R3;
using Alice;

namespace Core.LargeSatan {



    public class Attack2State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;

        [SerializeField] float speed = 20f;
        [SerializeField] float duration = 0.5f;
        [SerializeField] float endSpeedRatio = 0.1f;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 10;

        [SerializeField] AttackPlayer attackPlayer;
        [SerializeField] float impact = 5;

        [SerializeField] float waitDuration = 0.8f;

        Vector3 initialVelocity;
        float elapsedTime;
        bool hasDamaged;

        public override void OnEnter(IStrikerContext context) {

            context.PlayAnimation(animationClip);

            ScheduleStateEvent(waitDuration, nextContext => {
                nextContext.TryTransition(nextNode);
            });


            var direction = context.Rigidbody.transform.forward;
            initialVelocity = speed * direction;
            elapsedTime = 0f;
            context.Rigidbody.linearVelocity = initialVelocity;
            hasDamaged = false;

            attackPlayer.OnFilterHit
                .Subscribe(collider => {
                    if (!collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                        hurtbox = collider.GetComponent<Hurtbox>();
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
                        hurtbox = hit.Collider.GetComponent<Hurtbox>();
                        if (!hurtbox) {
                            return AttackPlayer.HitType.Cancel;
                        }
                    }

                    var dmg = 0f;
                    if (!hasDamaged && hurtbox.TryGetComponent<StrikerHub>(out _)) {
                        dmg = this.damage;
                        hasDamaged = true;
                    }

                    var hitStatus = new HitStatus(dmg, nockbackSpeed * initialVelocity.normalized);
                    var hitResult = hurtbox.GiveHit(hitStatus);
                    context.GenerateImpact(new StrikerImpact(impact * Vector3.down));

                    return hitResult.status == HitResult.Status.Guarded
                        ? AttackPlayer.HitType.Blocked
                        : AttackPlayer.HitType.Normal;
                })
                .AddTo(disposables);

            attackPlayer.Emit();
        }

        public override void OnHit(IStrikerStateContext hub, HitStatus status) {
            hub.PreventGroup();
            hub.ApplyDamage(status.Damage);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            elapsedTime += Time.deltaTime;
            float ratio = Mathf.Max(endSpeedRatio, 0.0001f);
            float decayRate = Mathf.Log(1f / ratio) / Mathf.Max(duration, 0.0001f);
            float decay = Mathf.Exp(-decayRate * elapsedTime);
            context.Rigidbody.linearVelocity = initialVelocity * decay;
        }

        public override void OnExit(IStrikerContext context) {
            hasDamaged = false;
        }

    }
}


