using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public enum AiStrikerPattern {
        Any,
        Hero,
        Wizard,
        Fighter,
        Warrior,
    }

    [Serializable]
    public class AiRegistryEntry {
        public string Id;
        public AiBrain BrainPrefab;
        public AiStrikerPattern Self = AiStrikerPattern.Any;
        public AiStrikerPattern Opponent = AiStrikerPattern.Any;
    }

    public record AiRegistration(string Id, AiBrain BrainPrefab, AiStrikerPattern Self, AiStrikerPattern Opponent);

    public interface IAIRegistry {
        AiRegistration Default { get; }
        bool TryGetById(string id, out AiRegistration registration);
        bool TryResolve(Striker self, Striker opponent, out AiRegistration registration);
        IReadOnlyList<AiRegistration> GetAll();
    }

    public class AIRegistry : MonoBehaviour, IAIRegistry {
        [Header("Fallback")]
        [SerializeField] string fallbackAiId;

        [SerializeField] AiRegistryEntry[] entries;

        readonly Dictionary<string, AiRegistration> registrationsById = new();
        readonly List<AiRegistration> allRegistrations = new();

        bool isInitialized;
        AiRegistration defaultRegistration;

        public AiRegistration Default {
            get {
                EnsureInitialized();
                return defaultRegistration;
            }
        }

        public bool TryGetById(string id, out AiRegistration registration) {
            EnsureInitialized();
            return registrationsById.TryGetValue(id, out registration);
        }

        public bool TryResolve(Striker self, Striker opponent, out AiRegistration registration) {
            EnsureInitialized();

            var resolved = defaultRegistration;
            var bestScore = int.MinValue;

            for (var i = 0; i < allRegistrations.Count; i++) {
                var candidate = allRegistrations[i];
                if (candidate.BrainPrefab == null) {
                    continue;
                }

                if (!Matches(candidate.Self, self) || !Matches(candidate.Opponent, opponent)) {
                    continue;
                }

                var score = MatchScore(candidate.Self, self) + MatchScore(candidate.Opponent, opponent);
                if (score <= bestScore) {
                    continue;
                }

                bestScore = score;
                resolved = candidate;
            }

            registration = resolved;
            return registration.BrainPrefab != null;
        }

        public IReadOnlyList<AiRegistration> GetAll() {
            EnsureInitialized();
            return allRegistrations;
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            registrationsById.Clear();
            allRegistrations.Clear();

            for (var i = 0; i < entries.Length; i++) {
                var entry = entries[i];
                var registration = new AiRegistration(entry.Id, entry.BrainPrefab, entry.Self, entry.Opponent);
                registrationsById[registration.Id] = registration;
                allRegistrations.Add(registration);
            }

            defaultRegistration = ResolveFallbackRegistration();
            isInitialized = true;
        }

        AiRegistration ResolveFallbackRegistration() {
            if (string.IsNullOrWhiteSpace(fallbackAiId)) {
                return new AiRegistration(string.Empty, null, AiStrikerPattern.Any, AiStrikerPattern.Any);
            }

            if (registrationsById.TryGetValue(fallbackAiId, out var registration)) {
                return registration;
            }

            Debug.LogError($"AI fallback id was not found in registry. fallbackAiId={fallbackAiId}", this);
            return new AiRegistration(string.Empty, null, AiStrikerPattern.Any, AiStrikerPattern.Any);
        }

        static bool Matches(AiStrikerPattern pattern, Striker striker) {
            return pattern switch {
                AiStrikerPattern.Any => true,
                AiStrikerPattern.Hero => striker == Striker.Hero,
                AiStrikerPattern.Wizard => striker == Striker.Wizard,
                AiStrikerPattern.Fighter => striker == Striker.Fighter,
                AiStrikerPattern.Warrior => striker == Striker.Warrior,
                _ => false,
            };
        }

        static int MatchScore(AiStrikerPattern pattern, Striker striker) {
            if (pattern == AiStrikerPattern.Any) {
                return 1;
            }

            return Matches(pattern, striker) ? 10 : 0;
        }
    }
}
