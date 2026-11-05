using Alice;
using R3;
using UnityEngine;

namespace Core.DarkSatan {
    public sealed class DemonDirectionalAttackState : StrikerState {
        public enum Technique { Uppercut, HeelDrop }
        [SerializeField] Technique technique;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] GroundChecker groundChecker;
        [SerializeField] StrikerAnimationClip animation;
        [SerializeField] DemonMeleeLimb limb;
        [SerializeField] DemonAttackEffect attackEffect;
        [SerializeField] EffectPlayer hitEffect, explosionEffect;
        [SerializeField, Min(.01f)] float explosionScale = .45f;
        [SerializeField] AudioClip swingSound, hitSound, blockedSound;
        [SerializeField, Min(.01f)] float duration = .4f;
        [SerializeField] float takeoffTime = .055f, diveTime = .16f;
        [SerializeField] float jumpSpeed = 22f, diveSpeed = 26f, forwardSpeed = 4f;
        [SerializeField] float hitStart = .08f, hitEnd = .29f;
        [SerializeField] float hitReach = .6f;
        [SerializeField] float damage = 12f, knockbackSpeed = 14f, impact = 1.8f;
        float elapsed;
        bool tookOff, diving, previousGravity, completed;
        Vector3 direction;
        public override StrikerStateCategory Category => StrikerStateCategory.Attack;

        public float ObservationDuration => duration;
        public float ObservationTakeoffSeconds => takeoffTime;
        public float ObservationDiveSeconds => diveTime;
        public float ObservationJumpSpeed => jumpSpeed;
        public float ObservationDiveSpeed => diveSpeed;
        public float ObservationForwardSpeed => forwardSpeed;
        public float ObservationHitStart => hitStart;
        public float ObservationHitEnd => hitEnd;
        public float ObservationDamage => damage;

        public override void OnEnter(IStrikerContext context) {
            elapsed = 0f;
            tookOff = diving = false;
            completed = false;
            previousGravity = context.Rigidbody.useGravity;
            context.Rigidbody.useGravity = true;
            direction = context.GetOpponent().Position.CurrentValue.x >= context.Rigidbody.position.x ? Vector3.right : Vector3.left;
            context.Rigidbody.rotation = Quaternion.LookRotation(direction);
            context.PlayAnimation(animation);
            limb.OnContact.Subscribe(contact => {
                var knockback = technique == Technique.Uppercut ? (direction * .35f + Vector3.up).normalized : (direction * .4f + Vector3.down).normalized;
                var result = contact.Hurtbox.GiveHit(new HitStatus(damage, knockback * knockbackSpeed));
                (result.status == HitResult.Status.Guarded ? blockedSound : hitSound).PlayAtApp(contact.Position);
                hitEffect.Emit(contact.Position, Quaternion.LookRotation(direction), Vector3.one * 1.8f);
                explosionEffect.Emit(contact.Position, Quaternion.identity, Vector3.one * explosionScale);
                context.GenerateImpact(new StrikerImpact(Vector3.down * impact));
            }).AddTo(disposables);
            limb.PlayEnergy(context.Rigidbody.transform, direction, hitStart, hitEnd, hitReach);
            attackEffect.Play(duration);
            swingSound.PlayAtApp(limb.transform.position);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            elapsed += Time.deltaTime;
            limb.SetAttackTime(elapsed);
            if (!tookOff && elapsed >= takeoffTime) {
                tookOff = true;
                var velocity = context.Rigidbody.linearVelocity;
                velocity.x = direction.x * forwardSpeed;
                if (technique == Technique.Uppercut || groundChecker.IsGrounded) velocity.y = jumpSpeed;
                context.Rigidbody.linearVelocity = velocity;
                hitEffect.Emit(context.Rigidbody.position + Vector3.up * .1f, Quaternion.identity, Vector3.one * 1.5f);
                context.GenerateImpact(new StrikerImpact(Vector3.down * 1.5f));
            }
            if (technique == Technique.HeelDrop && !diving && elapsed >= diveTime) {
                diving = true;
                context.Rigidbody.linearVelocity = direction * forwardSpeed + Vector3.down * diveSpeed;
            }
            if (elapsed >= duration) {
                completed = true;
                limb.SampleHitWindow();
                context.PreventGroup();
                context.TryTransition(nextNode);
            }
        }
        public override void OnAttackRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnDashRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnGuardRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnChargeRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnSpecialRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnExit(IStrikerContext context) {
            limb.StopEnergy();
            attackEffect.Stop();
            context.Rigidbody.useGravity = previousGravity;
            if (completed) {
                var velocity = context.Rigidbody.linearVelocity;
                velocity.x = 0f;
                context.Rigidbody.linearVelocity = velocity;
            }
        }
    }
}
