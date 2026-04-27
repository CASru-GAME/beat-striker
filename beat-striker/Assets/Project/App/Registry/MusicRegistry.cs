using System.Collections.Generic;
using System.Globalization;
using UnityEngine.AddressableAssets;
using UnityEngine;

namespace Alice {
    public static class BeatDataParser {
        public static IEnumerable<float> ParseBeatTimes(TextAsset beatData) {
            if (beatData == null || string.IsNullOrWhiteSpace(beatData.text)) {
                yield break;
            }

            var lines = beatData.text.Split('\n');
            for (var i = 0; i < lines.Length; i++) {
                var value = lines[i].Trim();
                if (string.IsNullOrEmpty(value) || value.StartsWith("#", System.StringComparison.Ordinal)) {
                    continue;
                }

                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var beatTime)) {
                    continue;
                }

                yield return beatTime;
            }
        }

        public static int CalculateBpm(TextAsset beatData) {
            var previousTime = 0f;
            var hasPrevious = false;
            var intervalSum = 0f;
            var intervalCount = 0;

            foreach (var beatTime in ParseBeatTimes(beatData)) {
                if (hasPrevious) {
                    var interval = beatTime - previousTime;
                    if (interval > 0f) {
                        intervalSum += interval;
                        intervalCount += 1;
                    }
                }

                previousTime = beatTime;
                hasPrevious = true;
            }

            if (intervalCount == 0 || intervalSum <= 0f) {
                return 0;
            }

            var averageInterval = intervalSum / intervalCount;
            var bpm = 60f / averageInterval;
            return Mathf.RoundToInt(bpm);
        }
    }

    [System.Serializable]
    public class AppMusicEntry {
        public string Id;
        public string DisplayName;
        public string Composer;
        [TextArea]
        public string Description;
        public AssetReferenceT<AudioClip> AudioClipReference;
        public AssetReferenceT<TextAsset> SpectrumDataReference;
        public AssetReferenceT<TextAsset> BeatDataReference;
    }

    public record MusicInfo(
        string Id,
        string DisplayName,
        string Composer,
        string Description,
        AssetReferenceT<AudioClip> AudioClipReference,
        AssetReferenceT<TextAsset> SpectrumDataReference,
        AssetReferenceT<TextAsset> BeatDataReference);

    public interface IMusicRegistry {
        MusicInfo Default { get; }
        MusicInfo GetById(string id);
        IReadOnlyList<MusicInfo> GetAll();
    }

    public class MusicRegistry : MonoBehaviour, IMusicRegistry {
        [SerializeField] AppMusicEntry[] musicEntries;

        readonly Dictionary<string, MusicInfo> musicById = new Dictionary<string, MusicInfo>();
        readonly List<MusicInfo> allMusic = new List<MusicInfo>();

        bool isInitialized;
        MusicInfo defaultMusic;

        public MusicInfo Default {
            get {
                EnsureInitialized();
                return defaultMusic;
            }
        }

        public MusicInfo GetById(string id) {
            EnsureInitialized();
            return musicById[id];
        }

        public IReadOnlyList<MusicInfo> GetAll() {
            EnsureInitialized();
            return allMusic;
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            musicById.Clear();
            allMusic.Clear();
            foreach (var entry in musicEntries) {
                var music = new MusicInfo(
                    entry.Id,
                    entry.DisplayName,
                    entry.Composer,
                    entry.Description,
                    entry.AudioClipReference,
                    entry.SpectrumDataReference,
                    entry.BeatDataReference);
                musicById[music.Id] = music;
                allMusic.Add(music);
            }

            defaultMusic = allMusic[0];
            isInitialized = true;
        }

    }
}
