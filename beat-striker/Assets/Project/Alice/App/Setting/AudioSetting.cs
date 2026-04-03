using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Alice {
    public record BeatOffsetSetting(float CommandTimeOffset, float ViewTimeOffset, float BeatTimeOffset);
    public record VolumeBalance(float MasterVolume, float BgmVolume, float SeVolume);

    [System.Serializable]
    public class Track {
        public string Name;
        public AudioClip AudioClip;
        public float bpm = 110;
        [Tooltip("ビートの時間オフセット（秒）。全ビート時刻に加算されます。値を大きくするとビートが後方（遅れ）に移動し、負にすると前方（早く）に移動します。曲の先頭の空白や音声ファイルのタイミング補正に使用します。")]
        public float offset = 0;
        public float[] beats;
        public AudioClip beatSound;
    }

    public interface IAudioSetting {
        Track GetTrack(string trackId);
        ReadOnlyReactiveProperty<float> CommandTimeOffset { get; }
        ReadOnlyReactiveProperty<float> ViewTimeOffset { get; }
        ReadOnlyReactiveProperty<float> BeatTimeOffset { get; }
        ReadOnlyReactiveProperty<float> PerfectWindow { get; }
        ReadOnlyReactiveProperty<BeatOffsetSetting> BeatOffset { get; }
        ReadOnlyReactiveProperty<VolumeBalance> VolumeBalance { get; }
        void SetBeatOffset(BeatOffsetSetting beatOffsetSetting);
        void SetVolumeBalance(VolumeBalance volumeBalance);
    }

    public class AudioSetting : MonoBehaviour, IAudioSetting {
        [SerializeField] List<Track> tracks;
        [Tooltip("入力判定の時間補正（秒）。評価時に「評価用入力時刻 = 入力時刻 + この値」で比較します。正の値は入力を遅めに、負の値は早めに評価します（過去の入力を書き換えるわけではありません）。例: 入力が約0.05秒遅れて登録される環境では -0.05 を設定して補正できます。")]
        [SerializeField] float commandTimeOffset = 0;
        [Tooltip("ノーツ描画の視覚補正（秒）。正の値を大きくするとノーツがヒット時刻より先に表示され（先行表示）、負にすると遅れて表示されます。画面遅延やノーツ移動時間の調整に使用します。")]
        [SerializeField] float viewTimeOffset = 0f;
        [Tooltip("ビート時刻を全体的に遅らせるオフセット（秒）。CalculateBeatsで生成される全ビートに加算されます。Track.offset はトラック個別の補正、beatTimeOffset はグローバル補正です。正の値でビートが遅れ、負の値で早まります。")]
        [SerializeField] float beatTimeOffset = 0;
        [SerializeField] float perfectWindow = 0.1f;
        [SerializeField] float masterVolume = 1f;
        [SerializeField] float bgmVolume = 1f;
        [SerializeField] float seVolume = 1f;

        readonly ReactiveProperty<float> commandTimeOffsetProperty = new();
        readonly ReactiveProperty<float> viewTimeOffsetProperty = new();
        readonly ReactiveProperty<float> beatTimeOffsetProperty = new();
        readonly ReactiveProperty<float> perfectWindowProperty = new();
        readonly ReactiveProperty<BeatOffsetSetting> beatOffset = new();
        readonly ReactiveProperty<VolumeBalance> volumeBalance = new();

        public Track GetTrack(string trackId) {
            var track = tracks.Find(t => t.Name == trackId) ?? tracks[0];
            track.beats = CalculateBeats(track.AudioClip.length, track.bpm, track.offset + beatTimeOffsetProperty.CurrentValue);
            return track;
        }

        public ReadOnlyReactiveProperty<float> CommandTimeOffset => commandTimeOffsetProperty;
        public ReadOnlyReactiveProperty<float> ViewTimeOffset => viewTimeOffsetProperty;
        public ReadOnlyReactiveProperty<float> BeatTimeOffset => beatTimeOffsetProperty;
        public ReadOnlyReactiveProperty<float> PerfectWindow => perfectWindowProperty;
        public ReadOnlyReactiveProperty<BeatOffsetSetting> BeatOffset => beatOffset;
        public ReadOnlyReactiveProperty<VolumeBalance> VolumeBalance => volumeBalance;

        void Awake() {
            commandTimeOffsetProperty.OnNext(commandTimeOffset);
            viewTimeOffsetProperty.OnNext(viewTimeOffset);
            beatTimeOffsetProperty.OnNext(beatTimeOffset);
            perfectWindowProperty.OnNext(perfectWindow);
            beatOffset.OnNext(new BeatOffsetSetting(commandTimeOffset, viewTimeOffset, beatTimeOffset));
            volumeBalance.OnNext(new VolumeBalance(masterVolume, bgmVolume, seVolume));
        }

        public void SetBeatOffset(BeatOffsetSetting beatOffsetSetting) {
            commandTimeOffset = beatOffsetSetting.CommandTimeOffset;
            viewTimeOffset = beatOffsetSetting.ViewTimeOffset;
            beatTimeOffset = beatOffsetSetting.BeatTimeOffset;
            commandTimeOffsetProperty.OnNext(commandTimeOffset);
            viewTimeOffsetProperty.OnNext(viewTimeOffset);
            beatTimeOffsetProperty.OnNext(beatTimeOffset);
            beatOffset.OnNext(beatOffsetSetting);
        }

        public void SetVolumeBalance(VolumeBalance nextVolumeBalance) {
            masterVolume = nextVolumeBalance.MasterVolume;
            bgmVolume = nextVolumeBalance.BgmVolume;
            seVolume = nextVolumeBalance.SeVolume;
            volumeBalance.OnNext(nextVolumeBalance);
        }

        float[] CalculateBeats(float trackLength, float bpm, float offset) {
            var beatInterval = 60f / bpm;
            var beatCount = Mathf.FloorToInt(trackLength / beatInterval);
            var beats = new float[beatCount];
            for (int i = 0; i < beatCount; i++) {
                beats[i] = i * beatInterval + offset;
            }
            return beats;
        }
    }
}
