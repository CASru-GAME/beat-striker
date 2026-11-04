using System;
using System.Linq;
using Alice;
using R3;
using UnityEngine;

namespace Core.DarkSatan {
    [Serializable]
    public sealed record DemonMeleeStrike {
        public string Name;
        public StrikerAnimationClip Animation;
        public DemonMeleeLimb Limb;
        [Range(0.1f, 0.4f)] public float Duration = 0.3f;
        [Min(0f)] public float HitStart = 0.07f;
        [Min(0.01f)] public float HitEnd = 0.18f;
        [Min(0f)] public float ChainStart = 0.24f;
        [Min(0f)] public float Damage = 10f;
        [Min(0f)] public float KnockbackSpeed = 7f;
        [Min(0f)] public float KnockbackLift = 0.15f;
        [Min(0f)] public float JumpHeight;
        [Min(0f)] public float StepSpeed = 5f;
        [Min(0.01f)] public float StepDuration = 0.12f;
        [Min(0.01f)] public float StepBrakeDuration = 0.06f;
        [Min(0f)] public float Impact = 0.5f;
        [Min(0.1f)] public float EffectScale = 1.5f;
        public AudioClip SwingSound;
        public AudioClip HitSound;
    }

    public sealed class DemonMeleeAttackState : StrikerState {
        [SerializeField] StrikerNode nextNode;
        [SerializeField] DemonChargeAttackState chargeState;
        [SerializeField] GroundChecker groundChecker;
        [SerializeField] DemonAttackEffect attackEffect;
        [SerializeField] DemonMeleeStrike[] strikes;
        [SerializeField, Min(0.4f)] float comboLinkSeconds = 0.85f;
        [SerializeField, Min(0f)] float stepStopDistance = 0.8f;
        [SerializeField] EffectPlayer hitEffect, explosionEffect;
        [SerializeField, Min(.01f)] float explosionScale = .45f;
        [SerializeField] AudioClip blockedSound;
        [SerializeField] StrikerNode upAttackState, downAttackState;

        int nextStrike;
        int currentStrike;
        float comboExpires;
        float elapsed;
        Vector3 direction;
        bool bufferedAttack;
        bool completed;
        bool stepping;
        bool jumping;
        StrikerNode bufferedDirectionalAttack;

        public override StrikerStateCategory Category => StrikerStateCategory.Attack;

        public override void OnEnter(IStrikerContext context) {
            if (Time.time > comboExpires) nextStrike = 0;
            foreach (var limb in strikes.Select(strike => strike.Limb).Distinct()) {
                limb.OnContact.Subscribe(contact => Hit(context, contact)).AddTo(disposables);
            }
            BeginStrike(context);
        }

        void BeginStrike(IStrikerContext context) {
            currentStrike = nextStrike;
            nextStrike = (currentStrike + 1) % strikes.Length;
            comboExpires = Time.time + comboLinkSeconds;
            elapsed = 0f;
            bufferedAttack = false;
            bufferedDirectionalAttack = null;
            completed = false;
            stepping = true;

            var toOpponent = context.GetOpponent().Position.CurrentValue - context.Rigidbody.position;
            direction = toOpponent.x >= 0f ? Vector3.right : Vector3.left;
            context.Rigidbody.rotation = Quaternion.LookRotation(direction);
            var strike = strikes[currentStrike];
            context.PlayAnimation(strike.Animation);
            strike.Limb.PlayEnergy(context.Rigidbody.transform, direction, strike.HitStart, strike.HitEnd);
            strike.SwingSound.PlayAtApp(strike.Limb.transform.position);
            ApplyStep(context, strike);
            jumping = strike.JumpHeight > 0f && groundChecker.IsGrounded;
            if (jumping) ApplyJump(context, strike);
            attackEffect.Play(strike.Duration);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            elapsed += Time.deltaTime;
            var strike = strikes[currentStrike];
            strike.Limb.SetAttackTime(elapsed);

            if (stepping) {
                ApplyStep(context, strike);
            }
            if (jumping) ApplyJump(context, strike);

            if (bufferedAttack && elapsed >= strike.ChainStart) {
                strike.Limb.SampleHitWindow();
                strike.Limb.StopEnergy();
                if (bufferedDirectionalAttack != null) {
                    context.PreventGroup();
                    context.TryTransition(bufferedDirectionalAttack);
                    return;
                }
                BeginStrike(context);
                context.PreventGroup();
                return;
            }
            if (elapsed >= strike.Duration) {
                strike.Limb.SampleHitWindow();
                completed = true;
                context.PreventGroup();
                context.TryTransition(nextNode);
            }
        }

        void ApplyJump(IStrikerContext context, DemonMeleeStrike strike) {
            // Complete the jump arc within the strike, including takeoff and landing.
            var velocity = context.Rigidbody.linearVelocity;
            velocity.y = 4f * strike.JumpHeight / strike.Duration * (1f - 2f * Mathf.Clamp01(elapsed / strike.Duration));
            context.Rigidbody.linearVelocity = velocity;
        }

        void ApplyStep(IStrikerContext context, DemonMeleeStrike strike) {
            float braking = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - strike.StepDuration) / strike.StepBrakeDuration));
            float speed = strike.StepSpeed * (1f - braking);
            float opponentDistance = Vector3.Dot(context.GetOpponent().Position.CurrentValue - context.Rigidbody.position, direction);
            float remainingDistance = Mathf.Max(0f, opponentDistance - stepStopDistance);
            speed = Mathf.Min(speed, remainingDistance / Mathf.Max(Time.deltaTime, Time.fixedDeltaTime));
            var velocity = context.Rigidbody.linearVelocity;
            velocity.x = direction.x * speed;
            context.Rigidbody.linearVelocity = velocity;
            stepping = elapsed < strike.StepDuration + strike.StepBrakeDuration && remainingDistance > 0f;
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
            if (chargeState.HasVolley) return;
            context.PreventGroup();
            bufferedAttack = true;
            bufferedDirectionalAttack = context.InputDirection.y > .5f ? upAttackState : context.InputDirection.y < -.5f ? downAttackState : null;
        }

        void Hit(IStrikerContext context, DemonMeleeContact contact) {
            var strike = strikes[currentStrike];
            var knockback = (direction + Vector3.up * strike.KnockbackLift).normalized;
            var result = contact.Hurtbox.GiveHit(new HitStatus(strike.Damage, knockback * strike.KnockbackSpeed));
            (result.status == HitResult.Status.Guarded ? blockedSound : strike.HitSound).PlayAtApp(contact.Position);
            hitEffect.Emit(contact.Position, Quaternion.LookRotation(direction), Vector3.one * strike.EffectScale);
            explosionEffect.Emit(contact.Position, Quaternion.identity, Vector3.one * explosionScale);
            context.GenerateImpact(new StrikerImpact(strike.Impact * Vector3.down));
        }

        public override void OnExit(IStrikerContext context) {
            attackEffect.Stop();
            strikes[currentStrike].Limb.StopEnergy();
            bufferedAttack = false;
            if (!completed) {
                nextStrike = 0;
                comboExpires = 0f;
            }
        }
    }
}
