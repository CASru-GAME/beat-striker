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

        public void Configure(int level) {
            (scoreVariation, anticipationWeight, alternativeChance, maxAlternativeScoreGap) = Mathf.Clamp(level, 1, 4) switch {
                1 => (12f, .5f, .60f, 40f),
                2 => (8f, .7f, .35f, 25f),
                3 => (4f, .85f, .15f, 12f),
                _ => (0f, 1f, 0f, 0f)
            };
        }

        internal (TCandidate Candidate, float Score) SelectCandidate<TCandidate>(
            IReadOnlyList<TCandidate> candidates,
            Predicate<TCandidate> isActive,
            Func<TCandidate, GamePadButton?> buttonFor,
            Func<TCandidate, float, float> evaluate) where TCandidate : class {
            scores.Clear();
            var bestIndex = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < candidates.Count; i++) {
                var candidate = candidates[i];
                var active = isActive(candidate);
                var score = AdjustScore(evaluate(candidate, active ? anticipationWeight : 1f), active);
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
