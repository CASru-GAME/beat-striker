
using Core.App.Types;
using System.Collections.Generic;

namespace Core.Battle {
    public interface IRythmTrackModelGetter {
        public float GetNextBeatTime(PlayerId playerId, int offset);
        public float GetTime();
    }

    public interface IRythmTrackModel: IRythmTrackModelGetter {
        BeatResult Beat(PlayerId playerId);
        List<PlayerId> SetTime(float time);
        void Reset();
    }
}