
using Core.App.Types;

namespace Core.Battle {
    public interface IRythmTrackModelGetter {
        public float GetNextBeatTime(PlayerId playerId, int offset);
        public float GetTime();
    }

    public interface IRythmTrackModel: IRythmTrackModelGetter {
        BeatResult Beat(PlayerId playerId);
        void AddTime(float time);
        void Reset();
    }
}