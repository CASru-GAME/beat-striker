
using Core.App.Types;

namespace Core.Battle {
    public interface IBattleView {
        void PlayTrack(TrackId trackId);
        void StopTrack();
        void SetRythmTrackModel(IRythmTrackModel model);
        bool IsPlaying();
        float GetAudioTime();
    }
}
