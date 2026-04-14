using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using Core.Striker.Components;


namespace Core.LargeHero {
    
    /// <summary>
    /// </summary>
    public class AttackState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] StrikerNode comboNode;   // 次の斬撃ステート（斬撃3なら空）
        [SerializeField] AttackPlayer attackPlayer;
        [SerializeField] float moveSpeed = 3;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 3;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip, OnAnimationEnd);

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

                    var hitpoint = hit.Collider.ClosestPoint(attackPlayer.transform.position);
                    var nockBackDirection = Mathf.Sign(hitpoint.x - context.Rigidbody.transform.position.x) * Vector2.right;
                    var hitResult = hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));

                    return hitResult.status == HitResult.Status.Guarded
                        ? AttackPlayer.HitType.Blocked
                        : AttackPlayer.HitType.Normal;
                })
                .AddTo(disposables);

            attackPlayer.Emit();
        }

        void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.Rigidbody.linearVelocity = moveSpeed * context.InputDirection;
        }

        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            context.PreventGroup();
            context.TryTransition(comboNode);
        }


    }
}
