using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public sealed class AiDifficulty {
        readonly System.Random random = new(Guid.NewGuid().GetHashCode());
        readonly List<float> scores = new(20);
        float scoreVariation;
        float anticipationWeight = 1f;
        float alternativeChance;
        float maxAlternativeScoreGap;
        float missChance;
        float defenseChance = 1f;

        public float PredictionWeight { get; private set; } = 1f;
        public float ThreatWeight { get; private set; } = 1f;
        public float FollowUpWeight { get; private set; } = 1f;
        public bool UsesAdvancedTactics { get; private set; }

        public bool MissesBeat() => missChance > 0f && random.NextDouble() < missChance;

        public void Configure(int level) {
            UsesAdvancedTactics = Mathf.Clamp(level, 1, 4) == 4;
            (scoreVariation, anticipationWeight, alternativeChance, maxAlternativeScoreGap) = Mathf.Clamp(level, 1, 4) switch {
                1 => (18f, 0f, .65f, 50f),
                2 => (12f, .08f, .45f, 35f),
                3 => (2f, .9f, .05f, 6f),
                _ => (0f, 1f, 0f, 0f)
            };
            (missChance, defenseChance, PredictionWeight, ThreatWeight, FollowUpWeight) = Mathf.Clamp(level, 1, 4) switch {
                1 => (.10f, .06f, 0f, .15f, 0f),
                2 => (.05f, .12f, .1f, .3f, 0f),
                3 => (0f, 1f, .9f, 1f, .5f),
                _ => (0f, 1f, 1f, 1f, 1f)
            };
        }

        internal (TCandidate Candidate, float Score) SelectCandidate<TCandidate>(
            IReadOnlyList<TCandidate> candidates,
            Predicate<TCandidate> isActive,
            Func<TCandidate, GamePadButton?> buttonFor,
            Predicate<TCandidate> isDefensive,
            Func<TCandidate, float, float> evaluate) where TCandidate : class {
            scores.Clear();
            // Roll once for the entire decision; extra dodge directions must not increase defense frequency.
            var allowDefense = defenseChance >= 1f || random.NextDouble() < defenseChance;
            var bestIndex = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < candidates.Count; i++) {
                var candidate = candidates[i];
                var active = isActive(candidate);
                var score = !allowDefense && isDefensive(candidate) ? float.NegativeInfinity
                    : AdjustScore(evaluate(candidate, anticipationWeight), active);
                scores.Add(score);
                if (score > bestScore) {
                    bestIndex = i;
                    bestScore = score;
                }
            }
            if (bestIndex < 0) return (null, 0f);

            var best = candidates[bestIndex];
            var bestButton = buttonFor(best);
            if (alternativeChance <= 0f || !bestButton.HasValue || random.NextDouble() >= alternativeChance)
                return (best, bestScore);

            // Give up a strong opening for a different move, without introducing an idle beat.
            var alternativeIndex = -1;
            var alternativeScore = bestScore - maxAlternativeScoreGap;
            for (var i = 0; i < candidates.Count; i++) {
                var button = buttonFor(candidates[i]);
                if (!button.HasValue || button == bestButton || button == GamePadButton.Start) continue;
                if (scores[i] > alternativeScore) {
                    alternativeIndex = i;
                    alternativeScore = scores[i];
                }
            }
            return alternativeIndex >= 0 ? (candidates[alternativeIndex], alternativeScore) : (best, bestScore);
        }

        float AdjustScore(float score, bool isActive) {
            // Vary close choices without making waiting outrank an action that already beat it.
            if (!isActive || scoreVariation <= 0f || float.IsInfinity(score) || float.IsNaN(score)) return score;
            return score + (float)random.NextDouble() * scoreVariation;
        }
    }
}
