using System.Collections.Generic;
using App;

namespace Alice {
    public record PlayerRoundRank(PlayerId PlayerId, int Rank);

    public record RoundResult(int RoundNumber, IReadOnlyList<PlayerRoundRank> Rankings);

    public record BattleJudgeResult(
        RoundResult RoundResult,
        bool ContinueBattle,
        PlayerId Winner,
        IReadOnlyDictionary<PlayerId, int> RoundWins);

    public interface IBattleJudge {
        BattleJudgeResult Judge(RoundResult roundResult);
        IReadOnlyDictionary<PlayerId, int> GetRoundWins();
        void ApplyRoundWins(IReadOnlyDictionary<PlayerId, int> sourceRoundWins);
    }

    public class BattleJudge : IBattleJudge {
        readonly Dictionary<PlayerId, int> roundWins = new();
        readonly IBattleRuleSetting battleRuleSetting;

        public BattleJudge(IBattleRuleSetting battleRuleSetting) {
            this.battleRuleSetting = battleRuleSetting;
        }

        public BattleJudgeResult Judge(RoundResult roundResult) {
            var roundsToWin = battleRuleSetting.RoundsToWin.CurrentValue < 1 ? 1 : battleRuleSetting.RoundsToWin.CurrentValue;
            var winner = roundResult.Rankings[0].PlayerId;
            if (!roundWins.ContainsKey(winner)) {
                roundWins[winner] = 0;
            }

            roundWins[winner] += 1;
            var continueBattle = roundWins[winner] < roundsToWin;
            PlayerId battleWinner = continueBattle ? (PlayerId)null : winner;

            return new BattleJudgeResult(
                roundResult,
                continueBattle,
                battleWinner,
                new Dictionary<PlayerId, int>(roundWins));
        }

        public IReadOnlyDictionary<PlayerId, int> GetRoundWins() {
            return new Dictionary<PlayerId, int>(roundWins);
        }

        public void ApplyRoundWins(IReadOnlyDictionary<PlayerId, int> sourceRoundWins) {
            roundWins.Clear();
            foreach (var roundWin in sourceRoundWins) {
                roundWins[roundWin.Key] = roundWin.Value;
            }
        }
    }
}
