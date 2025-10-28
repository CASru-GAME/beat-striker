
namespace Core.Battle {
    public class BattleRule {
        private int ExcellentScore;
        private int GoodScore;

        public BattleRule(int excellentScore, int goodScore) {
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