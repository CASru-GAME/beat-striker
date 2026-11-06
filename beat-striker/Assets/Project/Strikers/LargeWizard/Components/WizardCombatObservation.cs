using UnityEngine;
using System.Collections.Generic;

namespace Core.LargeWizard {
    // Authored on the character prefab. Brains only read combat data through this component.
    public sealed class WizardCombatObservation : MonoBehaviour {
        [SerializeField] EnergyStorage energyStorage;
        [SerializeField] ChargeState charge;
        [SerializeField] Attack1State fire;
        [SerializeField] Attack2State ice;
        [SerializeField] Attack3State rock;
        [SerializeField] DashState dash;
        [SerializeField] JumpUpwardState jump;
        [SerializeField] GuardState guard;
        [SerializeField] SpecialState special;

        public int Energy => energyStorage.StoredEnergy;
        public float ChargeSeconds => charge.ObservationDuration;
        public float FireReleaseSeconds => fire.ObservationReleaseSeconds;
        public Fire FirePrefab => fire.ObservationPrefab;
        public float FireLifetime => fire.ObservationLifetime;
        public float IceReleaseSeconds => ice.ObservationReleaseSeconds;
        public Ice IcePrefab => ice.ObservationPrefab;
        public void CollectActiveIce(List<Ice> results) => ice.CollectActiveIce(results);
        public float RockReleaseSeconds => rock.ObservationReleaseSeconds;
        public Rock RockPrefab => rock.ObservationPrefab;
        public float RockSpawnHeight => rock.ObservationSpawnHeight;
        public Vector3 FireOrigin => fire.ObservationOrigin;
        public float DashSpeed => dash.ObservationSpeed;
        public float JumpSpeed => jump.ObservationSpeed;
        public float ShieldSpeed => guard.ObservationLaunchSpeed;
        public float ShieldDamage => guard.ObservationLaunchDamage;
        public float ShieldLifetime => guard.ObservationLaunchLifetime;
        public Vector3 ShieldLaunchPosition => guard.ObservationLaunchPosition;
        public float SpecialRange => special.ObservationRange;
        public float SpecialDelay => special.ObservationBeamDelay;
        public Vector3 SpecialOrigin => special.ObservationOrigin;

        public float PendingReleaseSeconds(IStrikerState state) => state switch {
            ChargeState => ChargeSeconds,
            Attack1State => FireReleaseSeconds,
            Attack2State => IceReleaseSeconds,
            Attack3State => RockReleaseSeconds,
            _ => 0f
        };
    }
}
