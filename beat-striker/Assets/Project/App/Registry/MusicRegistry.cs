using System.Collections.Generic;
using System.Globalization;
using UnityEngine.AddressableAssets;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

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
        public AssetReferenceT<AudioClip> PreviewAudioClipReference;
        public AssetReferenceT<TextAsset> SpectrumDataReference;
        public TextAsset BeatData;
        [Min(0)]
        public int PrecomputedBpm;
        [Min(0f)]
        public float PrecomputedLengthSeconds;
    }

    public record MusicInfo(
        string Id,
        string DisplayName,
        string Composer,
        string Description,
        int Bpm,
        float LengthSeconds);

    public interface IMusicRegistry {
        MusicInfo Default { get; }
        MusicInfo GetById(string id);
        IReadOnlyList<MusicInfo> GetAll();
        Awaitable<LoadedAsset<AudioClip>> LoadAudioClipAsync(string id);
        Awaitable<LoadedAsset<AudioClip>> LoadPreviewAudioClipAsync(string id);
        Awaitable<LoadedAsset<TextAsset>> LoadSpectrumDataAsync(string id);
        Awaitable<LoadedAsset<TextAsset>> LoadBeatDataAsync(string id);
    }

    public class MusicRegistry : MonoBehaviour, IMusicRegistry {
        [SerializeField] AppMusicEntry[] musicEntries;

        readonly Dictionary<string, MusicInfo> musicById = new Dictionary<string, MusicInfo>();
        readonly Dictionary<string, AppMusicEntry> entryById = new Dictionary<string, AppMusicEntry>();
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

        public async Awaitable<LoadedAsset<AudioClip>> LoadAudioClipAsync(string id) {
            EnsureInitialized();
            if (!entryById.TryGetValue(id, out var entry)) {
                return LoadedAsset<AudioClip>.Empty();
            }

            return await LoadAssetAsync<AudioClip>(entry.AudioClipReference);
        }

        public async Awaitable<LoadedAsset<AudioClip>> LoadPreviewAudioClipAsync(string id) {
            EnsureInitialized();
            if (!entryById.TryGetValue(id, out var entry)) {
                return LoadedAsset<AudioClip>.Empty();
            }

            if (entry.PreviewAudioClipReference != null && entry.PreviewAudioClipReference.RuntimeKeyIsValid()) {
                return await LoadAssetAsync<AudioClip>(entry.PreviewAudioClipReference);
            }

            return await LoadAssetAsync<AudioClip>(entry.AudioClipReference);
        }

        public async Awaitable<LoadedAsset<TextAsset>> LoadSpectrumDataAsync(string id) {
            EnsureInitialized();
            if (!entryById.TryGetValue(id, out var entry)) {
                return LoadedAsset<TextAsset>.Empty();
            }

            return await LoadAssetAsync<TextAsset>(entry.SpectrumDataReference);
        }

#pragma warning disable CS1998
        public async Awaitable<LoadedAsset<TextAsset>> LoadBeatDataAsync(string id) {
            EnsureInitialized();
            if (!entryById.TryGetValue(id, out var entry) || entry.BeatData == null) {
                return LoadedAsset<TextAsset>.Empty();
            }

            return new LoadedAsset<TextAsset>(entry.BeatData);
        }
#pragma warning restore CS1998

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            musicById.Clear();
            entryById.Clear();
            allMusic.Clear();
            foreach (var entry in musicEntries) {
                var music = new MusicInfo(
                    entry.Id,
                    entry.DisplayName,
                    entry.Composer,
                    entry.Description,
                    entry.PrecomputedBpm,
                    entry.PrecomputedLengthSeconds);
                musicById[music.Id] = music;
                entryById[music.Id] = entry;
                allMusic.Add(music);
            }

            defaultMusic = allMusic[0];
            isInitialized = true;
        }

        static async Awaitable<LoadedAsset<T>> LoadAssetAsync<T>(AssetReferenceT<T> assetReference) where T : UnityEngine.Object {
            if (assetReference == null || !assetReference.RuntimeKeyIsValid()) {
                return LoadedAsset<T>.Empty();
            }

            var handle = Addressables.LoadAssetAsync<T>(assetReference);
            while (!handle.IsDone) {
                await Awaitable.NextFrameAsync();
            }

            if (handle.Status != AsyncOperationStatus.Succeeded) {
                if (handle.IsValid()) {
                    Addressables.Release(handle);
                }
                return LoadedAsset<T>.Empty();
            }

            return new LoadedAsset<T>(handle.Result, handle);
        }

    }
}
