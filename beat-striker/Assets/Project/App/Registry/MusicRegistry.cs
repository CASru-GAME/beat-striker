using System.Collections.Generic;
using System.Globalization;
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
    }

    [System.Serializable]
    public class AppMusicEntry {
        public string Id;
        public string DisplayName;
        public string Composer;
        [TextArea]
        public string Description;
        public AudioClip AudioClip;
        public TextAsset SpectrumData;
        public TextAsset BeatData;
    }

    public record MusicInfo(
        string Id,
        string DisplayName,
        string Composer,
        int Bpm,
        string Description,
        AudioClip AudioClip,
        TextAsset SpectrumData,
        TextAsset BeatData);

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
                var bpm = CalculateBpm(entry.BeatData);
                var music = new MusicInfo(entry.Id, entry.DisplayName, entry.Composer, bpm, entry.Description, entry.AudioClip, entry.SpectrumData, entry.BeatData);
                musicById[music.Id] = music;
                allMusic.Add(music);
            }

            defaultMusic = allMusic[0];
            isInitialized = true;
        }

        static int CalculateBpm(TextAsset beatData) {
            var previousTime = 0f;
            var hasPrevious = false;
            var intervalSum = 0f;
            var intervalCount = 0;

            foreach (var beatTime in BeatDataParser.ParseBeatTimes(beatData)) {
                if (hasPrevious) {
                    var interval = beatTime - previousTime;
                    if (interval > 0f) {
                        intervalSum += interval;
                        intervalCount++;
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
}
