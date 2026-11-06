using UnityEngine;

namespace Core.LargeSatan {
    // Read-only settings of the authored combo and its actual next-state link.
    public interface IWarriorMeleeObservation {
        float ObservationSpeed { get; }
        float ObservationDuration { get; }
        float ObservationEndSpeedRatio { get; }
        float ObservationDamage { get; }
        Vector2 ObservationHitWindow { get; }
        Bounds ObservationBounds { get; }
        IWarriorMeleeObservation ObservationNextAttack { get; }
    }
}
