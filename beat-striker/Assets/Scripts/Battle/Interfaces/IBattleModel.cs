

using Core.App.Types;

namespace Core.Battle {
    public interface IBattleModel: IBattlemodelGetter {
        void NextRound();
        void AddLoser(PlayerId playerId);
    }

    public interface IBattlemodelGetter {
        PlayerId GetWinner(int round);
        int GetWinCount(PlayerId playerId);
        int GetCurrentRound();
        bool IsFinished();
        PlayerId GetFinalWinner();
    }
}