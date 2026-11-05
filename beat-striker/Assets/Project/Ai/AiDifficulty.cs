using System;
using UnityEngine;

namespace Alice {
    public sealed class AiDifficulty {
        readonly System.Random random = new(Guid.NewGuid().GetHashCode());
        float scoreVariation;

        public float AnticipationWeight { get; private set; } = 1f;

        public void Configure(int level) {
            (scoreVariation, AnticipationWeight) = Mathf.Clamp(level, 1, 4) switch {
                1 => (12f, .5f),
                2 => (8f, .7f),
                3 => (4f, .85f),
                _ => (0f, 1f)
            };
        }

        public float AdjustScore(float score, bool isActive) {
            // Vary close choices without making waiting outrank an action that already beat it.
            if (!isActive || scoreVariation <= 0f || float.IsInfinity(score) || float.IsNaN(score)) return score;
            return score + (float)random.NextDouble() * scoreVariation;
        }
    }
}
