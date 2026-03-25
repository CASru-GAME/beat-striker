

using UnityEngine;

namespace Core.Battle {

    public readonly struct HitStatus {
        public readonly float Damage;
        public readonly Vector3 KnockbackVelocity;
        
        public HitStatus(float damage) {
            this.Damage = damage;
            this.KnockbackVelocity = Vector2.zero;
        }

        public HitStatus(float damage, Vector3 knockback) {
            this.Damage = damage;
            this.KnockbackVelocity = knockback;
        }
    }
}