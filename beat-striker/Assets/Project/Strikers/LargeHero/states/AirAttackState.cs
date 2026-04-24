using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using Core.Striker.Components;


namespace Core.LargeHero {

    public class AirAttackState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] AttackPlayer attackPlayer;

        [SerializeField] TrailRenderer swordTrail;

        [SerializeField] float damage = 10;
        [SerializeField] float nockbackSpeed = 10;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip, OnAnimationEnd);
            swordTrail.Clear();
            swordTrail.enabled = true;

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

        public void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        public override void OnUpdate(IStrikerStateContext context) {}

        public override void OnExit(IStrikerContext context) {
            swordTrail.enabled = false;
            swordTrail.Clear();
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
