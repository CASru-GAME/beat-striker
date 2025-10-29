

using Core.App.Types;

namespace Core.Battle {

    public interface IStrikerModel {
        public PlayerId PlayerId { get; }
        public HitPoint MaxHitPoint { get; }
        public HitPoint HitPoint { get; }
        public SpecialPoint SpecialPoint { get; }
        public int MissCount { get; }
        public int GoodCount { get; }
        public int ExcellentCount { get; }
        public int Score { get; }
        public int ComboCount { get; }

        public void TakeDamage(HitPoint damage);
        public void Heal(HitPoint heal);
        public void GainSpecial(SpecialPoint gain);
        public void AddBeatResult(BeatResult result);
        public bool IsDead();
    }
}