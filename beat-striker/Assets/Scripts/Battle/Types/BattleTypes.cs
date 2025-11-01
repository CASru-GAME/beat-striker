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
        OnDead,
        OnIntro,
        OnVictory
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

    public struct HitStatus {
        public HitPoint damage;
        
        public HitStatus(HitPoint damage) {
            this.damage = damage;
        }
    }
}