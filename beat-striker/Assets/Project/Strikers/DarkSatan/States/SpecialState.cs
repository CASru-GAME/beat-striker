using Alice;
using UnityEngine;

namespace Core.DarkSatan {
    public sealed class SpecialState : StrikerState {
        enum Phase { Landing, Attention, PushOff, Sprint, Brake }
        [SerializeField] StrikerAnimationClip chargeAnimation, launchAnimation, sprintAnimation, brakeAnimation;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] Transform visual;
        [SerializeField] CapsuleCollider bodyCollider;
        [SerializeField] ParticleSystem aura, rushWind, launchBurst;
        [SerializeField] GameObject manaShell;
        [SerializeField] DemonAttackEffect attackEffect;
        [SerializeField] EffectPlayer explosionEffect;
        [SerializeField, Min(.01f)] float explosionScale = 1.7f;
        [SerializeField] AudioClip explosionSound;
        [SerializeField] AudioClip hitSound, blockedSound, launchSound;
        [SerializeField] string attentionTechniqueText = "魔王・覇王突進";
        [SerializeField, Min(.01f)] float attentionSeconds = 1.25f, growthSeconds = .48f, pushOffSeconds = .16f, rushSeconds = .72f, recoverySeconds = .32f;
        [SerializeField] float giantScale = 2.5f, rushSpeed = 26f, accelerationSeconds = .16f;
        [SerializeField] float hitRadius = 1.65f, hitHeight = 1.8f;
        [SerializeField] float damage = 70f, knockbackSpeed = 27f, impact = 7f;
        [SerializeField] float attentionZoomDistance = 6f;
        Vector3 originalScale, direction, previousCenter;
        float elapsed, phaseElapsed;
        bool previousGravity, hasHit, cameraReleasing, completed;
        int activation;
        Phase phase;
        CollisionDetectionMode previousCollisionMode;
        public override StrikerStateCategory Category => StrikerStateCategory.Special;

        public StrikerProjectileObservation ObserveRush(Vector3 position) {
            if (hasHit || phase == Phase.Landing || phase == Phase.Brake) return null;
            var delay = phase == Phase.Attention && !cameraReleasing ? Mathf.Max(0f, attentionSeconds - elapsed) : 0f;
            var remaining = phase == Phase.Sprint ? Mathf.Max(0f, rushSeconds - phaseElapsed) : pushOffSeconds + rushSeconds;
            return new StrikerProjectileObservation(position + Vector3.up * hitHeight + direction * .4f,
                direction * rushSpeed, hitRadius + .4f, delay, delay + remaining);
        }
        public override void OnEnter(IStrikerContext context) {
            elapsed = phaseElapsed = 0f;
            hasHit = cameraReleasing = false;
            completed = false;
            phase = Phase.Landing;
            originalScale = visual.localScale;
            previousGravity = context.Rigidbody.useGravity;
            previousCollisionMode = context.Rigidbody.collisionDetectionMode;
            context.Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            context.Rigidbody.useGravity = true;
            var velocity = context.Rigidbody.linearVelocity;
            velocity.x = 0f; velocity.z = 0f;
            context.Rigidbody.linearVelocity = velocity;
            direction = context.GetOpponent().Position.CurrentValue.x >= context.Rigidbody.position.x ? Vector3.right : Vector3.left;
            context.Rigidbody.rotation = Quaternion.LookRotation(direction);
            previousCenter = context.Rigidbody.position + Vector3.up * hitHeight;
            ++activation;
        }
        void BeginAttention(IStrikerStateContext context) {
            phase = Phase.Attention;
            previousCenter = context.Rigidbody.position + Vector3.up * hitHeight;
            elapsed = phaseElapsed = 0f;
            int current = activation;
            context.RequestAttention(new AttentionRequest(attentionSeconds, attentionTechniqueText,
                OnReturnStarted: () => { if (activation == current) cameraReleasing = true; }, ZoomDistance: attentionZoomDistance, ResumeOnReturnStarted: true));
            context.PlayAnimation(chargeAnimation);
            aura.Play(true);
            manaShell.SetActive(true);
            rushWind.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            attackEffect.Play(attentionSeconds + 5f);
        }
        bool IsOnGround(IStrikerStateContext context) {
            var bounds = bodyCollider.bounds;
            foreach (var hit in Physics.RaycastAll(bounds.center, Vector3.down, bounds.extents.y + .1f, ~0, QueryTriggerInteraction.Ignore)) {
                if (!hit.collider.transform.IsChildOf(context.Rigidbody.transform) && hit.normal.y > .5f) return true;
            }
            return false;
        }
        public override void OnUpdate(IStrikerStateContext context) {
            if (phase == Phase.Landing) {
                if (!IsOnGround(context) || context.Rigidbody.linearVelocity.y > .1f) return;
                BeginAttention(context);
            }
            elapsed += Time.deltaTime;
            phaseElapsed += Time.deltaTime;
            if (phase == Phase.Attention) {
                visual.localScale = originalScale * Mathf.Lerp(1, giantScale, Mathf.SmoothStep(0,1,elapsed/growthSeconds));
                var velocity = context.Rigidbody.linearVelocity;
                velocity.x = 0f; velocity.z = 0f;
                context.Rigidbody.linearVelocity = velocity;
                if (!cameraReleasing) return;
                phase = Phase.PushOff; phaseElapsed = 0f;
                context.PlayAnimation(launchAnimation);
                launchBurst.Play(true);
                rushWind.Play(true);
                launchSound.PlayAtApp(context.Rigidbody.position);
                context.GenerateImpact(new StrikerImpact(Vector3.down * 2.5f));
            }
            if (phase == Phase.PushOff && phaseElapsed >= pushOffSeconds) {
                phase = Phase.Sprint; phaseElapsed = 0;
                context.PlayAnimation(sprintAnimation);
            }
            if (phase == Phase.Sprint && phaseElapsed >= rushSeconds) {
                phase = Phase.Brake; phaseElapsed = 0;
                context.PlayAnimation(brakeAnimation);
                rushWind.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            var center = context.Rigidbody.position + Vector3.up * hitHeight;
            if ((phase == Phase.PushOff || phase == Phase.Sprint) && !hasHit) {
                foreach (var collider in Physics.OverlapCapsule(previousCenter, center + direction * .8f, hitRadius, ~0, QueryTriggerInteraction.Collide)) {
                    if (collider.transform.IsChildOf(context.Rigidbody.transform) || !collider.TryGetComponent<Hurtbox>(out var hurtbox)) continue;
                    hasHit = true;
                    var result = hurtbox.GiveHit(new HitStatus(damage, (direction * .5f + Vector3.up * 0.8660254f) * knockbackSpeed));
                    (result.status == HitResult.Status.Guarded ? blockedSound : hitSound).PlayAtApp(center);
                    var hitPosition = collider.ClosestPoint(center);
                    explosionEffect.Emit(hitPosition, Quaternion.identity, Vector3.one * explosionScale);
                    explosionSound.PlayAtApp(hitPosition);
                    context.GenerateImpact(new StrikerImpact(Vector3.down * impact));
                    break;
                }
            }
            previousCenter = center;
            float speed = phase == Phase.PushOff ? rushSpeed * Mathf.SmoothStep(0,1,phaseElapsed/accelerationSeconds)
                : phase == Phase.Sprint ? rushSpeed : rushSpeed * (1-Mathf.Clamp01(phaseElapsed / (recoverySeconds * .5f)));
            var rushVelocity = context.Rigidbody.linearVelocity;
            rushVelocity.x = direction.x * speed; rushVelocity.z = 0f;
            context.Rigidbody.linearVelocity = rushVelocity;
            if (phase != Phase.Brake) return;
            visual.localScale = originalScale * Mathf.Lerp(giantScale,1,Mathf.SmoothStep(0,1,phaseElapsed/recoverySeconds));
            if (phaseElapsed >= recoverySeconds) {
                completed = true;
                context.PreventGroup(); context.TryTransition(nextNode);
            }
        }
        public override void OnAttackRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnDashRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnGuardRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnChargeRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnSpecialRequested(IStrikerStateContext context) => context.PreventGroup();
        public override void OnExit(IStrikerContext context) {
            activation++;
            manaShell.SetActive(false);
            visual.localScale = originalScale;
            foreach (var effect in new[]{aura,rushWind,launchBurst}) effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            attackEffect.Stop();
            context.Rigidbody.useGravity = previousGravity;
            context.Rigidbody.collisionDetectionMode = previousCollisionMode;
            if (completed) {
                var velocity = context.Rigidbody.linearVelocity;
                velocity.x = 0f; velocity.z = 0f;
                context.Rigidbody.linearVelocity = velocity;
            }
        }
    }
}
