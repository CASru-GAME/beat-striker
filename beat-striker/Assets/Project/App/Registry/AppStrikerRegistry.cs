using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Alice {
    [System.Serializable]
    public class AppStrikerEntry {
        public string DisplayName;
        public Striker BattleStriker;
        public AssetReferenceGameObject PrefabReference;
        public AssetReferenceGameObject PreviewModelReference;
        public Sprite Portrait;
    }

    public record StrikerInfo(string DisplayName, Striker BattleStriker, AssetReferenceGameObject PrefabReference, AssetReferenceGameObject PreviewModelReference, Sprite Portrait);
    public record PlayerStrikerSelection(int PlayerId, StrikerInfo Striker);

    public interface IAppStrikerRegistry {
        StrikerInfo Default { get; }
        StrikerInfo GetByStriker(Striker striker);
        IReadOnlyList<StrikerInfo> GetAll();
    }

    public class AppStrikerRegistry : MonoBehaviour, IAppStrikerRegistry {
        [SerializeField] AppStrikerEntry[] strikerEntries;

        readonly Dictionary<Striker, StrikerInfo> strikerByType = new Dictionary<Striker, StrikerInfo>();
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

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            strikerByType.Clear();
            allStrikers.Clear();
            foreach (var entry in strikerEntries) {
                var strikerInfo = new StrikerInfo(entry.DisplayName, entry.BattleStriker, entry.PrefabReference, entry.PreviewModelReference, entry.Portrait);
                strikerByType[strikerInfo.BattleStriker] = strikerInfo;
                allStrikers.Add(strikerInfo);
            }

            defaultStriker = allStrikers[0];
            isInitialized = true;
        }
    }
}
