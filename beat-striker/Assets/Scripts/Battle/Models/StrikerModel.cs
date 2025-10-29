
using Core.App.Types;

namespace Core.Battle {
    public class StrikerModel : IStrikerModel {
        public PlayerId PlayerId { get; private set; }
        public HitPoint HitPoint { get; private set; }
        public SpecialPoint SpecialPoint { get; private set; } = new(0);
        public HitPoint MaxHitPoint { get; private set; }
        public int MissCount { get; private set; } = 0;
        public int GoodCount { get; private set; } = 0;
        public int ExcellentCount { get; private set; } = 0;
        public int Score { get; private set; } = 0;
        public int ComboCount { get; private set; } = 0;
        private ScoreRule rule;

        public StrikerModel(PlayerId playerId, HitPoint hitPoint, ScoreRule rule) {
            this.PlayerId = playerId;
            this.MaxHitPoint = hitPoint;
            this.HitPoint = hitPoint;
            this.rule = rule;
        }

        public void TakeDamage(HitPoint damage) {
            var nextHp = HitPoint.value - damage.value;
            HitPoint newHp = new(nextHp < 0 ? 0 : nextHp > MaxHitPoint.value ? MaxHitPoint.value : nextHp);
            HitPoint = newHp;
        }

        public void Heal(HitPoint heal) {
            var nextHp = HitPoint.value + heal.value;
            HitPoint newHp = new(nextHp < 0 ? 0 : nextHp > MaxHitPoint.value ? MaxHitPoint.value : nextHp);
            HitPoint = newHp;
        }

        public void GainSpecial(SpecialPoint gain) {
            var nextSp = SpecialPoint.value + gain.value;
            SpecialPoint newSp = new(nextSp < 0 ? 0 : nextSp);
            SpecialPoint = newSp;
        }

        public void AddBeatResult(BeatResult result) {
            if (result.status == BeatStatus.Miss) {
                MissCount++;
                ComboCount = 0;
                Score += rule.GetScoreForJudge(BeatStatus.Miss);
            }
            else if (result.status == BeatStatus.Good) {
                GoodCount++;
                Score += rule.GetScoreForJudge(BeatStatus.Good);
                ComboCount++;
            }
            else if (result.status == BeatStatus.Excellent) {
                ExcellentCount++;
                Score += rule.GetScoreForJudge(BeatStatus.Excellent);
                ComboCount++;
            }
        }
        
        public bool IsDead() {
            return HitPoint.value <= 0;
        }
    }
}