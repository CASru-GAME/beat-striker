using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Alice {
    public sealed class LoadedAsset<T> : IDisposable where T : UnityEngine.Object {
        readonly AsyncOperationHandle<T> handle;
        readonly bool hasHandle;
        bool disposed;

        public T Asset { get; }

        public LoadedAsset(T asset, AsyncOperationHandle<T> handle) : this(asset, handle, true) {
        }

        LoadedAsset(T asset, AsyncOperationHandle<T> handle, bool hasHandle) {
            Asset = asset;
            this.handle = handle;
            this.hasHandle = hasHandle;
        }

        public static LoadedAsset<T> Empty() {
            return new LoadedAsset<T>(null, default, false);
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            if (hasHandle && handle.IsValid()) {
                Addressables.Release(handle);
            }
        }
    }

    [System.Serializable]
    public class AppStrikerEntry {
        public string DisplayName;
        public Striker BattleStriker;
        public AssetReferenceGameObject PrefabReference;
        public AssetReferenceGameObject PreviewModelReference;
        public Sprite Portrait;
    }

    public record StrikerInfo(string DisplayName, Striker BattleStriker, Sprite Portrait);
    public record PlayerStrikerSelection(int PlayerId, StrikerInfo Striker);

    public interface IAppStrikerRegistry {
        StrikerInfo Default { get; }
        StrikerInfo GetByStriker(Striker striker);
        IReadOnlyList<StrikerInfo> GetAll();
        LoadedAsset<GameObject> LoadBattlePrefab(Striker striker);
        LoadedAsset<GameObject> LoadPreviewModel(Striker striker);
    }

    public class AppStrikerRegistry : MonoBehaviour, IAppStrikerRegistry {
        [SerializeField] AppStrikerEntry[] strikerEntries;

        readonly Dictionary<Striker, StrikerInfo> strikerByType = new Dictionary<Striker, StrikerInfo>();
        readonly Dictionary<Striker, AppStrikerEntry> entryByType = new Dictionary<Striker, AppStrikerEntry>();
        readonly List<StrikerInfo> allStrikers = new List<StrikerInfo>();

        bool isInitialized;
        StrikerInfo defaultStriker;

        public StrikerInfo Default {
            get {
                EnsureInitialized();
                return defaultStriker;
            }
        }

        public StrikerInfo GetByStriker(Striker striker) {
            EnsureInitialized();
            return strikerByType[striker];
        }

        public IReadOnlyList<StrikerInfo> GetAll() {
            EnsureInitialized();
            return allStrikers;
        }

        public LoadedAsset<GameObject> LoadBattlePrefab(Striker striker) {
            EnsureInitialized();
            return !entryByType.TryGetValue(striker, out var entry)
                ? LoadedAsset<GameObject>.Empty()
                : LoadAsset<GameObject>(entry.PrefabReference);
        }

        public LoadedAsset<GameObject> LoadPreviewModel(Striker striker) {
            EnsureInitialized();
            return !entryByType.TryGetValue(striker, out var entry)
                ? LoadedAsset<GameObject>.Empty()
                : LoadAsset<GameObject>(entry.PreviewModelReference);
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            strikerByType.Clear();
            entryByType.Clear();
            allStrikers.Clear();
            foreach (var entry in strikerEntries) {
                var strikerInfo = new StrikerInfo(entry.DisplayName, entry.BattleStriker, entry.Portrait);
                strikerByType[strikerInfo.BattleStriker] = strikerInfo;
                entryByType[strikerInfo.BattleStriker] = entry;
                allStrikers.Add(strikerInfo);
            }

            defaultStriker = allStrikers[0];
            isInitialized = true;
        }

        static LoadedAsset<T> LoadAsset<T>(AssetReferenceT<T> assetReference) where T : UnityEngine.Object {
            if (assetReference == null || !assetReference.RuntimeKeyIsValid()) {
                return LoadedAsset<T>.Empty();
            }

            var handle = Addressables.LoadAssetAsync<T>(assetReference);
            return new LoadedAsset<T>(handle.WaitForCompletion(), handle);
        }
    }
}
