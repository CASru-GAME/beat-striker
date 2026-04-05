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
    }

    public class BattleJudge : IBattleJudge {
        readonly Dictionary<PlayerId, int> roundWins = new();
        readonly int roundsToWin;

        public BattleJudge(IBattleRuleSetting battleRuleSetting) {
            roundsToWin = battleRuleSetting.RoundsToWin.CurrentValue < 1 ? 1 : battleRuleSetting.RoundsToWin.CurrentValue;
        }

        public BattleJudgeResult Judge(RoundResult roundResult) {
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
    }
}
