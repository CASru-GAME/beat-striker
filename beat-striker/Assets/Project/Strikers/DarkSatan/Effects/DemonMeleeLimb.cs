using R3;
using UnityEngine;

namespace Core.DarkSatan {
    public sealed record DemonMeleeContact(Hurtbox Hurtbox, Vector3 Position);

    [RequireComponent(typeof(SphereCollider))]
    public sealed class DemonMeleeLimb : MonoBehaviour {
        [SerializeField] SphereCollider hitCollider;
        [SerializeField] ParticleSystem energy;
        [SerializeField] TrailRenderer trail;
        [SerializeField] Light glow;
        [SerializeField, Min(0f)] float energyReach = 0.3f;

        readonly Subject<DemonMeleeContact> onContact = new();
        Transform owner;
        Vector3 previousPosition;
        Vector3 direction;
        float attackTime, previousTime, hitStart, hitEnd;
        bool attacking;
        bool hasHit;
        float activeReach;

        public Observable<DemonMeleeContact> OnContact => onContact;

        void Awake() => StopEnergy();

        public void PlayEnergy(Transform striker, Vector3 forward, float start, float end, float? reach = null) {
            owner = striker;
            direction = forward;
            hitStart = start;
            hitEnd = end;
            activeReach = reach ?? energyReach;
            attackTime = previousTime = 0f;
            previousPosition = transform.TransformPoint(hitCollider.center);
            attacking = true;
            hasHit = false;
            energy.Play(true);
            trail.Clear();
            trail.emitting = true;
            glow.enabled = true;
        }

        public void SetAttackTime(float elapsed) => attackTime = elapsed;

        public void StopEnergy() {
            attacking = false;
            hitCollider.enabled = false;
            energy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            trail.emitting = false;
            trail.Clear();
            glow.enabled = false;
        }

        void LateUpdate() => SampleHitWindow();

        public void SampleHitWindow() {
            if (!attacking) return;
            var position = transform.TransformPoint(hitCollider.center);
            // Sweep the previous and current animated poses even if a frame spans the hit window.
            bool overlapsWindow = previousTime <= hitEnd && attackTime >= hitStart;
            hitCollider.enabled = attackTime >= hitStart && attackTime <= hitEnd;
            if (!overlapsWindow || hasHit) {
                previousPosition = position;
                previousTime = attackTime;
                return;
            }
            var scale = transform.lossyScale;
            float radius = hitCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var movement = position - previousPosition;
            if (movement.sqrMagnitude > 0.000001f) {
                var contacts = Physics.CapsuleCastAll(previousPosition, previousPosition + direction * activeReach, radius, movement.normalized, movement.magnitude, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(contacts, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var contact in contacts) {
                    var hitPosition = contact.distance <= 0f ? contact.collider.ClosestPoint(previousPosition) : contact.point;
                    if (TryHit(contact.collider, hitPosition)) break;
                }
            }
            if (!hasHit) {
                foreach (var collider in Physics.OverlapCapsule(position, position + direction * activeReach, radius, ~0, QueryTriggerInteraction.Collide)) {
                    if (TryHit(collider, collider.ClosestPoint(position))) break;
                }
            }
            previousPosition = position;
            previousTime = attackTime;
        }

        bool TryHit(Collider collider, Vector3 position) {
            if (collider.transform.IsChildOf(owner)) return false;
            if (!collider.TryGetComponent<Hurtbox>(out var hurtbox)) return false;
            hasHit = true;
            onContact.OnNext(new DemonMeleeContact(hurtbox, position));
            return true;
        }

        void OnDisable() => StopEnergy();
        void OnDestroy() => onContact.Dispose();
    }
}
