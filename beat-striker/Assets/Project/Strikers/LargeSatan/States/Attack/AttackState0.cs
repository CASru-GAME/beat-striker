using UnityEngine;
using R3;
using Alice;

namespace Core.LargeSatan {



    public class Attack0State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode, nextAttackNode;

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

        public override void OnEnter(IStrikerContext context) {

            ScheduleStateEvent(waitDuration, nextContext => {
                nextContext.TryTransition(nextNode);
            });

            context.PlayAnimation(animationClip);

            var toOpponent = context.GetOpponent().Position.CurrentValue - context.Rigidbody.position;
            if (Vector3.Dot(context.Rigidbody.transform.forward, toOpponent) < 0) {
                context.Rigidbody.rotation *= Quaternion.Euler(0, 180, 0);
            }

            var direction = context.Rigidbody.transform.forward;
            if(context.InputDirection.x * direction.x < 0) {
                direction = Vector3.up;
            }
            else if(context.InputDirection.y > 0) {
                direction = context.InputDirection;
            } 
            initialVelocity = speed * direction;
            elapsedTime = 0f;
            context.Rigidbody.linearVelocity = initialVelocity;

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

                    var hitStatus = new HitStatus(damage, nockbackSpeed * initialVelocity.normalized);
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
            float ratio = Mathf.Max(endSpeedRatio, 0.0001f);
            float decayRate = Mathf.Log(1f / ratio) / Mathf.Max(duration, 0.0001f);
            float decay = Mathf.Exp(-decayRate * elapsedTime);
            context.Rigidbody.linearVelocity = initialVelocity * decay;
        }


        public override void OnAttackRequested(IStrikerStateContext hub) {
            hub.PreventGroup();
            hub.TryTransition(nextAttackNode);
        }


    }
}


