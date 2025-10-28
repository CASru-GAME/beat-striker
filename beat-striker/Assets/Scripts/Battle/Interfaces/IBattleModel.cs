

using Core.App.Types;

namespace Core.Battle {
    public interface IBattleModel {
        PlayerId GetWinner(int round);
        int GetCurrentRound();
        void NextRound();
        void AddLoser(PlayerId playerId);
        bool IsFinished();
        PlayerId GetFinalWinner();
    }
}