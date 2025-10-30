
using Core.App.Types;

namespace Core.Battle {
    public interface IRythmTrackModelGetter {
        public float GetBeatTime(PlayerId playerId, int index);
        public float GetTime();
    }

    public interface IRythmTrackModel: IRythmTrackModelGetter {
        BeatResult Beat(PlayerId playerId);
        void AddTime(float time);
    }
}