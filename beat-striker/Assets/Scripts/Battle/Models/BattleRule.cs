
using System;

namespace Core.Battle {
    public class ScoreRule {
        private int ExcellentScore;
        private int GoodScore;

        public ScoreRule(int excellentScore, int goodScore) {
            this.ExcellentScore = excellentScore;
            this.GoodScore = goodScore;
        }

        public int GetScoreForJudge(BeatStatus status) {
            return status switch {
                BeatStatus.Excellent => ExcellentScore,
                BeatStatus.Good => GoodScore,
                _ => 0,
            };
        }
    }
}