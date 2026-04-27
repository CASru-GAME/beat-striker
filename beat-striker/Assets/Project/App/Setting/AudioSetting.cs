using R3;
using UnityEngine;
using UnityEngine.Serialization;

namespace Alice {
    public record BeatOffsetSetting(float CommandTimeOffset, float ViewTimeOffset, float BeatTimeOffset);
    public record VolumeBalance(float MasterVolume, float BgmVolume, float SeVolume);

    public interface IAudioSetting {
        ReadOnlyReactiveProperty<float> CommandTimeOffset { get; }
        ReadOnlyReactiveProperty<float> ViewTimeOffset { get; }
        ReadOnlyReactiveProperty<float> BeatTimeOffset { get; }
        float MinimumPlaybackSeconds { get; }
        ReadOnlyReactiveProperty<float> GoodWindow { get; }
        ReadOnlyReactiveProperty<float> ExcellentWindow { get; }
        ReadOnlyReactiveProperty<float> GoodScoreMultiplier { get; }
        ReadOnlyReactiveProperty<float> ExcellentScoreMultiplier { get; }
        ReadOnlyReactiveProperty<BeatOffsetSetting> BeatOffset { get; }
        ReadOnlyReactiveProperty<VolumeBalance> VolumeBalance { get; }
        void SetBeatOffset(BeatOffsetSetting beatOffsetSetting);
        void SetVolumeBalance(VolumeBalance volumeBalance);
    }

    public class AudioSetting : MonoBehaviour, IAudioSetting {
        const string LOG_PREFIX = "[AudioSetting]";
        [Tooltip("入力判定の時間補正（秒）。評価時に「評価用入力時刻 = 入力時刻 + この値」で比較します。正の値は入力を遅めに、負の値は早めに評価します（過去の入力を書き換えるわけではありません）。例: 入力が約0.05秒遅れて登録される環境では -0.05 を設定して補正できます。")]
        [SerializeField] float commandTimeOffset = 0;
        [Tooltip("ノーツ描画の視覚補正（秒）。正の値を大きくするとノーツがヒット時刻より先に表示され（先行表示）、負にすると遅れて表示されます。画面遅延やノーツ移動時間の調整に使用します。")]
        [SerializeField] float viewTimeOffset = 0f;
        [Tooltip("ビートデータの全時刻に加算するグローバル補正（秒）。正の値でビートが遅れ、負の値で早まります。")]
        [SerializeField] float beatTimeOffset = 0;
        [FormerlySerializedAs("perfectWindow")]
        [SerializeField] float goodWindow = 0.1f;
        [SerializeField] float excellentWindow = 0.05f;
        [SerializeField] float goodScoreMultiplier = 1f;
        [SerializeField] float excellentScoreMultiplier = 1.5f;
        [SerializeField] float masterVolume = 1f;
        [SerializeField] float bgmVolume = 1f;
        [SerializeField] float seVolume = 1f;
        [Tooltip("短い曲をループ延長して確保する最小再生時間(秒)。0以下で延長なし。")]
        [Min(0f)]
        [FormerlySerializedAs("minimumInfiniteModePlaybackSeconds")]
        [SerializeField] float minimumPlaybackSeconds = 60f * 7f;

        readonly ReactiveProperty<float> commandTimeOffsetProperty = new();
        readonly ReactiveProperty<float> viewTimeOffsetProperty = new();
        readonly ReactiveProperty<float> beatTimeOffsetProperty = new();
        readonly ReactiveProperty<float> goodWindowProperty = new();
        readonly ReactiveProperty<float> excellentWindowProperty = new();
        readonly ReactiveProperty<float> goodScoreMultiplierProperty = new();
        readonly ReactiveProperty<float> excellentScoreMultiplierProperty = new();
        readonly ReactiveProperty<BeatOffsetSetting> beatOffset = new(new BeatOffsetSetting(0f, 0f, 0f));
        readonly ReactiveProperty<VolumeBalance> volumeBalance = new(new VolumeBalance(1f, 1f, 1f));
        bool isInitialized;

        public ReadOnlyReactiveProperty<float> CommandTimeOffset {
            get {
                EnsureInitialized();
                return commandTimeOffsetProperty;
            }
        }
        public ReadOnlyReactiveProperty<float> ViewTimeOffset {
            get {
                EnsureInitialized();
                return viewTimeOffsetProperty;
            }
        }
        public ReadOnlyReactiveProperty<float> BeatTimeOffset {
            get {
                EnsureInitialized();
                return beatTimeOffsetProperty;
            }
        }
        public float MinimumPlaybackSeconds => minimumPlaybackSeconds;
        public ReadOnlyReactiveProperty<float> GoodWindow {
            get {
                EnsureInitialized();
                return goodWindowProperty;
            }
        }
        public ReadOnlyReactiveProperty<float> ExcellentWindow {
            get {
                EnsureInitialized();
                return excellentWindowProperty;
            }
        }
        public ReadOnlyReactiveProperty<float> GoodScoreMultiplier {
            get {
                EnsureInitialized();
                return goodScoreMultiplierProperty;
            }
        }
        public ReadOnlyReactiveProperty<float> ExcellentScoreMultiplier {
            get {
                EnsureInitialized();
                return excellentScoreMultiplierProperty;
            }
        }
        public ReadOnlyReactiveProperty<BeatOffsetSetting> BeatOffset {
            get {
                EnsureInitialized();
                return beatOffset;
            }
        }
        public ReadOnlyReactiveProperty<VolumeBalance> VolumeBalance {
            get {
                EnsureInitialized();
                return volumeBalance;
            }
        }

        void Awake() {
            EnsureInitialized();
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            commandTimeOffsetProperty.OnNext(commandTimeOffset);
            viewTimeOffsetProperty.OnNext(viewTimeOffset);
            beatTimeOffsetProperty.OnNext(beatTimeOffset);
            goodWindowProperty.OnNext(goodWindow);
            excellentWindowProperty.OnNext(excellentWindow);
            goodScoreMultiplierProperty.OnNext(goodScoreMultiplier);
            excellentScoreMultiplierProperty.OnNext(excellentScoreMultiplier);
            beatOffset.OnNext(new BeatOffsetSetting(commandTimeOffset, viewTimeOffset, beatTimeOffset));
            volumeBalance.OnNext(new VolumeBalance(masterVolume, bgmVolume, seVolume));
            isInitialized = true;
            Debug.Log($"{LOG_PREFIX} Initialized. command={commandTimeOffset:0.###}, view={viewTimeOffset:0.###}, beat={beatTimeOffset:0.###}");
        }

        public void SetBeatOffset(BeatOffsetSetting beatOffsetSetting) {
            EnsureInitialized();
            commandTimeOffset = beatOffsetSetting.CommandTimeOffset;
            viewTimeOffset = beatOffsetSetting.ViewTimeOffset;
            beatTimeOffset = beatOffsetSetting.BeatTimeOffset;
            commandTimeOffsetProperty.OnNext(commandTimeOffset);
            viewTimeOffsetProperty.OnNext(viewTimeOffset);
            beatTimeOffsetProperty.OnNext(beatTimeOffset);
            beatOffset.OnNext(beatOffsetSetting);
            Debug.Log($"{LOG_PREFIX} SetBeatOffset applied. command={commandTimeOffset:0.###}, view={viewTimeOffset:0.###}, beat={beatTimeOffset:0.###}");
        }

        public void SetVolumeBalance(VolumeBalance nextVolumeBalance) {
            masterVolume = nextVolumeBalance.MasterVolume;
            bgmVolume = nextVolumeBalance.BgmVolume;
            seVolume = nextVolumeBalance.SeVolume;
            volumeBalance.OnNext(nextVolumeBalance);
        }
    }
}
