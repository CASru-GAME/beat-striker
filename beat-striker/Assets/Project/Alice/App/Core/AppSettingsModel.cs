using System.Collections.Generic;
using R3;

namespace Alice {
    public interface IAppSettingsModel {
        ReadOnlyReactiveProperty<StageInfo> SelectedStage { get; }
        ReadOnlyReactiveProperty<MusicInfo> SelectedMusic { get; }
        ReadOnlyReactiveProperty<BeatOffsetSetting> BeatOffset { get; }
        ReadOnlyReactiveProperty<VolumeBalance> VolumeBalance { get; }
        void SelectStage(StageInfo stage);
        void SelectMusic(MusicInfo music);
        void SetBeatOffset(BeatOffsetSetting beatOffset);
        void SetVolumeBalance(VolumeBalance volumeBalance);
    }

    public class AppSettingsModel : IAppSettingsModel {
        readonly ReactiveProperty<StageInfo> selectedStage;
        readonly ReactiveProperty<MusicInfo> selectedMusic;
        readonly ReactiveProperty<BeatOffsetSetting> beatOffset;
        readonly ReactiveProperty<VolumeBalance> volumeBalance;

        public ReadOnlyReactiveProperty<StageInfo> SelectedStage => selectedStage;
        public ReadOnlyReactiveProperty<MusicInfo> SelectedMusic => selectedMusic;
        public ReadOnlyReactiveProperty<BeatOffsetSetting> BeatOffset => beatOffset;
        public ReadOnlyReactiveProperty<VolumeBalance> VolumeBalance => volumeBalance;

        public AppSettingsModel(StageInfo defaultStage, MusicInfo defaultMusic, BeatOffsetSetting defaultBeatOffset, VolumeBalance defaultVolumeBalance) {
            selectedStage = new ReactiveProperty<StageInfo>(defaultStage);
            selectedMusic = new ReactiveProperty<MusicInfo>(defaultMusic);
            beatOffset = new ReactiveProperty<BeatOffsetSetting>(defaultBeatOffset);
            volumeBalance = new ReactiveProperty<VolumeBalance>(defaultVolumeBalance);
        }

        public void SelectStage(StageInfo stage) {
            selectedStage.OnNext(stage);
        }

        public void SelectMusic(MusicInfo music) {
            selectedMusic.OnNext(music);
        }

        public void SetBeatOffset(BeatOffsetSetting nextBeatOffset) {
            beatOffset.OnNext(nextBeatOffset);
        }

        public void SetVolumeBalance(VolumeBalance nextVolumeBalance) {
            volumeBalance.OnNext(nextVolumeBalance);
        }
    }
}