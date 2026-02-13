

using UnityEngine;

namespace Core.Battle {
    public enum Anime {
        DoAttack,
        DoCharge,
        OnCharged,
        DoSpecial,
        DoGuard,
        IsGround,
        Velocity,
        InputX,
        InputY,
        MoveX,
        MoveY,
        IsRotation,
        OnMiss,
        OnHit,
        IsDead,
        OnIntro,
        OnVictory,
OnReset
    }

    public enum BeatStatus {
        Excellent,
        Good,
        Miss
    }

    public class BeatResult {
        public readonly BeatStatus status;
        public BeatResult(BeatStatus status) {
            this.status = status;
        }
    }

    public struct HitPoint {
        public float value;
        public HitPoint(float value) {
            this.value = value;
        }
    }

    public struct SpecialPoint {
        public float value;
        public SpecialPoint(float value) {
            this.value = value;
        }
    }

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