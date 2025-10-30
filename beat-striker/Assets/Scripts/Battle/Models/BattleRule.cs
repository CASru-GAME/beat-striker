
using System;

namespace Core.Battle {
    public class ScoreRule {
        private int ExcellentScore;
        private int GoodScore;
        private int SpecialGain;

        public ScoreRule(int excellentScore, int goodScore, int specialGain) {
            this.ExcellentScore = excellentScore;
            this.GoodScore = goodScore;
            this.SpecialGain = specialGain;
        }

        public int GetScoreForJudge(BeatStatus status) {
            return status switch {
                BeatStatus.Excellent => ExcellentScore,
                BeatStatus.Good => GoodScore,
                _ => 0,
            };
        }

        public int GetSpecialGain() {
            return SpecialGain;
        }
    }
}