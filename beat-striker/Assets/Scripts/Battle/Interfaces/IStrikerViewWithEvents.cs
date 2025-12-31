using Core.App.Interfaces;
using Core.App.Types;

namespace Core.Battle {
    /// <summary>
    /// Extended striker view interface that supports construction with BattleEvents.
    /// </summary>
    public interface IStrikerViewWithEvents : IStrikerView {
        IStrikerModelGetter Construct(PlayerId playerId, ScoreRule rule, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry, IBattleModel battleModel);
    }
}
