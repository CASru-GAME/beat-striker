using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public sealed class BattleAddressablePreload : IDisposable {
        readonly Dictionary<Striker, LoadedAsset<GameObject>> battlePrefabAssets = new();
        bool disposed;

        public string MusicId { get; private set; }
        public LoadedAsset<AudioClip> MusicClipAsset { get; private set; }
        public LoadedAsset<TextAsset> BeatDataAsset { get; private set; }

        public void AddBattlePrefab(Striker striker, LoadedAsset<GameObject> asset) {
            if (battlePrefabAssets.TryGetValue(striker, out var existing)) {
                existing.Dispose();
            }

            battlePrefabAssets[striker] = asset;
        }

        public bool TryGetBattlePrefab(Striker striker, out LoadedAsset<GameObject> asset) {
            return battlePrefabAssets.TryGetValue(striker, out asset);
        }

        public void SetMusic(string musicId, LoadedAsset<AudioClip> clipAsset, LoadedAsset<TextAsset> beatDataAsset) {
            MusicClipAsset?.Dispose();
            BeatDataAsset?.Dispose();
            MusicId = musicId;
            MusicClipAsset = clipAsset;
            BeatDataAsset = beatDataAsset;
        }

        public bool HasMusic(string musicId) {
            return !string.IsNullOrEmpty(MusicId) && MusicId == musicId;
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            foreach (var asset in battlePrefabAssets.Values) {
                asset.Dispose();
            }

            battlePrefabAssets.Clear();
            MusicClipAsset?.Dispose();
            MusicClipAsset = null;
            BeatDataAsset?.Dispose();
            BeatDataAsset = null;
            MusicId = null;
        }
    }
}
