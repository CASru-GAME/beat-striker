using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace Alice {
    public sealed class LoadedAsset<T> : IDisposable where T : UnityEngine.Object {
        readonly AsyncOperationHandle<T> handle;
        readonly bool hasHandle;
        bool disposed;

        public T Asset { get; }

        public LoadedAsset(T asset, AsyncOperationHandle<T> handle) : this(asset, handle, true) {
        }

        public LoadedAsset(T asset) : this(asset, default, false) {
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
        public Sprite Portrait;
        public Sprite Thumbnail;
    }

    public record StrikerInfo(string DisplayName, Striker BattleStriker, Sprite Portrait, Sprite Thumbnail);
    public record PlayerStrikerSelection(int PlayerId, StrikerInfo Striker);

    public interface IAppStrikerRegistry {
        StrikerInfo Default { get; }
        StrikerInfo GetByStriker(Striker striker);
        IReadOnlyList<StrikerInfo> GetAll();
        Awaitable<LoadedAsset<GameObject>> LoadBattlePrefabAsync(Striker striker);
    }

    public class AppStrikerRegistry : MonoBehaviour, IAppStrikerRegistry {
        [SerializeField] AppStrikerEntry[] strikerEntries;
        [SerializeField, Min(0f)] float debugLoadDelaySeconds;
        ILoadingOverlayService loadingOverlayService;

        readonly Dictionary<Striker, StrikerInfo> strikerByType = new Dictionary<Striker, StrikerInfo>();
        readonly Dictionary<Striker, AppStrikerEntry> entryByType = new Dictionary<Striker, AppStrikerEntry>();
        readonly List<StrikerInfo> allStrikers = new List<StrikerInfo>();

        bool isInitialized;
        StrikerInfo defaultStriker;

        [Inject]
        public void Construct(ILoadingOverlayService loadingOverlayService) {
            this.loadingOverlayService = loadingOverlayService;
        }

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

        public async Awaitable<LoadedAsset<GameObject>> LoadBattlePrefabAsync(Striker striker) {
            EnsureInitialized();
            if (!entryByType.TryGetValue(striker, out var entry)) {
                return LoadedAsset<GameObject>.Empty();
            }

            using var scope = loadingOverlayService.Begin();
            return await LoadAssetAsync<GameObject>(entry.PrefabReference, debugLoadDelaySeconds);
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            strikerByType.Clear();
            entryByType.Clear();
            allStrikers.Clear();
            foreach (var entry in strikerEntries) {
                var strikerInfo = new StrikerInfo(entry.DisplayName, entry.BattleStriker, entry.Portrait, entry.Thumbnail);
                strikerByType[strikerInfo.BattleStriker] = strikerInfo;
                entryByType[strikerInfo.BattleStriker] = entry;
                allStrikers.Add(strikerInfo);
            }

            defaultStriker = allStrikers[0];
            isInitialized = true;
        }

        async Awaitable<LoadedAsset<T>> LoadAssetAsync<T>(AssetReferenceT<T> assetReference, float loadDelaySeconds) where T : UnityEngine.Object {
            if (assetReference == null || !assetReference.RuntimeKeyIsValid()) {
                return LoadedAsset<T>.Empty();
            }

            if (loadDelaySeconds > 0f) {
                await Ex.WaitAsync(loadDelaySeconds);
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
