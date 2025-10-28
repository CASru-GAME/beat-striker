
using Core.App.Types;

namespace Core.Battle {
    public interface IRythmTrackModel {
        BeatResult Beat(PlayerId playerId);
        void AddTime(float time);
    }
}