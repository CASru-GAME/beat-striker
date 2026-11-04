using UnityEngine;

namespace Core.LargeHero {
    // References and combat settings remain editable on the character prefab.
    public sealed class HeroCombatObservation : MonoBehaviour {
        [SerializeField] Rigidbody body;
        [SerializeField] EnergyStorage energyStorage;
        [SerializeField] GroundChecker groundChecker;
        [SerializeField] ChargeState charge;
        [SerializeField] RushState rush;
        [SerializeField] BeamState beam;
        [SerializeField] JumpUPForwardState jump;
        [SerializeField] AirDashState airDash;
        [SerializeField] AirGroup airGroup;
        [SerializeField] Slash1State slash1;
        [SerializeField] Slash2State slash2;
        [SerializeField] Slash3State slash3;
        [SerializeField] AirAttackState airAttack;
        [SerializeField] SphereCollider slash1Volume;
        [SerializeField] SphereCollider slash2Volume;
        [SerializeField] SphereCollider slash3Volume;
        [SerializeField] BoxCollider shieldVolume;
        [SerializeField] GuardState guard;
        [SerializeField] SpecialApproachState approach;
        [SerializeField] SpecialLaunchState launch;
        [SerializeField] Bullet rangedProjectile;
        [SerializeField] Bullet rushProjectile;
        [SerializeField] BoxCollider rushVolume;

        public int Energy => energyStorage.StoredEnergy;
        public Vector3 PhysicsVelocity => body.linearVelocity;
        public bool Grounded => groundChecker.IsGrounded;
        public float ChargeSeconds => charge.ObservationDuration;
        public float RushSpeed => rush.ObservationSpeed;
        public float RushSeconds => rush.ObservationDuration;
        public Vector3 RushOrigin => rush.ObservationOrigin;
        public float RushDamage => rushProjectile.ObservationDamage;
        public float RushProjectileSpeed => rushProjectile.ObservationSpeed;
        public float? RushProjectileLifetimeSeconds => rush.ObservationProjectileLifetimeSeconds;
        public Bounds RushBounds(Vector3 origin, Vector3 forward) {
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var half = rushVolume.size * .5f;
            var bounds = new Bounds(origin, Vector3.zero);
            for (var i = 0; i < 8; i++) {
                var signs = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                var point = rushVolume.transform.TransformPoint(rushVolume.center + Vector3.Scale(half, signs));
                var local = rushProjectile.transform.InverseTransformPoint(point);
                var world = origin + rotation * Vector3.Scale(local, rushProjectile.transform.lossyScale);
                if (i == 0) bounds = new Bounds(world, Vector3.zero); else bounds.Encapsulate(world);
            }
            return bounds;
        }
        public float BeamReleaseSeconds => beam.ObservationReleaseSeconds;
        public Vector3 BeamOrigin => beam.ObservationOrigin;
        public float BeamSpeed => rangedProjectile.ObservationSpeed;
        public float BeamDamage => rangedProjectile.ObservationDamage;
        public float JumpSpeed => jump.ObservationSpeed;
        public float AirDashSpeed => airDash.ObservationSpeed;
        public float AirDamping => airGroup.ObservationDamping;
        public float GuardSeconds => guard.ObservationDuration;
        public float SpecialReach => approach.ObservationActivationDistance + approach.ObservationSpeed * approach.ObservationDuration;
        public float SpecialActivationDistance => approach.ObservationActivationDistance;
        public float SpecialApproachSpeed => approach.ObservationSpeed;
        public float SpecialApproachSeconds => approach.ObservationDuration;
        public Vector3 SpecialHitOffset => launch.ObservationHitOffset;
        public Vector3 SpecialHitHalfExtents => launch.ObservationHitHalfExtents;
        public bool ShieldActive => shieldVolume.gameObject.activeInHierarchy;

        public bool UsesAirInputs(IStrikerState state) {
            if (state == null) return false;
            foreach (var parent in state.Parents) if (parent is AirGroup) return true;
            return false;
        }

        public float NextMeleeDamage(IStrikerState state) => state switch {
            Slash1State => slash2.ObservationDamage,
            Slash2State => slash3.ObservationDamage,
            Slash3State => slash1.ObservationDamage,
            _ => UsesAirInputs(state) ? airAttack.ObservationDamage : slash1.ObservationDamage
        };

        public Bounds NextMeleeBounds(IStrikerState state) {
            var volume = state switch {
                Slash1State => slash2Volume,
                Slash2State => slash3Volume,
                Slash3State => slash1Volume,
                _ => UsesAirInputs(state) ? slash3Volume : slash1Volume
            };
            var scale = volume.transform.lossyScale;
            var radius = volume.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return new Bounds(volume.transform.TransformPoint(volume.center), Vector3.one * radius * 2f);
        }

        public Bounds ShieldBounds {
            get {
                var center = shieldVolume.center;
                var half = shieldVolume.size * .5f;
                var bounds = new Bounds(shieldVolume.transform.TransformPoint(center), Vector3.zero);
                for (var i = 0; i < 8; i++) {
                    var corner = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    bounds.Encapsulate(shieldVolume.transform.TransformPoint(center + Vector3.Scale(half, corner)));
                }
                return bounds;
            }
        }
    }
}
