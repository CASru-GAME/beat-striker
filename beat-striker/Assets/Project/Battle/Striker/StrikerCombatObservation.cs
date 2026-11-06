using UnityEngine;
using R3;

namespace Alice {
    // Optional, read-only combat detail for brains. IObservableStriker remains usable
    // by replicas and other consumers that only need the public character values.
    public interface IStrikerCombatObservationSource {
        IStrikerState ObservedState { get; }
        float StateElapsedSeconds { get; }
        int StateVersion { get; }
        Observable<IStrikerState> OnCombatStateEntered { get; }
        float NextGuardSeconds { get; }
        float ActiveGuardSeconds { get; }
        Bounds BodyBounds { get; }
        Bounds? MovementBounds { get; }
        Transform ObservationRoot { get; }
    }

    public record StrikerProjectileObservation(
        Vector3 Position,
        Vector3 Velocity,
        float Radius,
        float DelaySeconds = 0f,
        float LifetimeSeconds = 1f
    );

    public record StrikerAreaAttackObservation(Bounds Bounds, float StartsAt, float EndsAt);
}
