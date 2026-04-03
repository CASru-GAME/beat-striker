using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    [System.Serializable]
    public class AppStrikerEntry {
        public string Id;
        public string DisplayName;
        public Striker BattleStriker;
        public StrikerHub Prefab;
        public Sprite Portrait;
    }

    public record StrikerInfo(string Id, string DisplayName, Striker BattleStriker, StrikerHub Prefab, Sprite Portrait);
    public record PlayerStrikerSelection(int PlayerId, StrikerInfo Striker);

    public interface IAppStrikerRegistry {
        StrikerInfo Default { get; }
        StrikerInfo GetById(string id);
        IReadOnlyList<StrikerInfo> GetAll();
    }

    public class AppStrikerRegistry : MonoBehaviour, IAppStrikerRegistry {
        [SerializeField] AppStrikerEntry[] strikerEntries;

        readonly Dictionary<string, StrikerInfo> strikerById = new Dictionary<string, StrikerInfo>();
        readonly List<StrikerInfo> allStrikers = new List<StrikerInfo>();

        bool isInitialized;
        StrikerInfo defaultStriker;

        public StrikerInfo Default {
            get {
                EnsureInitialized();
                return defaultStriker;
            }
        }

        public StrikerInfo GetById(string id) {
            EnsureInitialized();
            return strikerById[id];
        }

        public IReadOnlyList<StrikerInfo> GetAll() {
            EnsureInitialized();
            return allStrikers;
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            strikerById.Clear();
            allStrikers.Clear();
            foreach (var entry in strikerEntries) {
                var striker = new StrikerInfo(entry.Id, entry.DisplayName, entry.BattleStriker, entry.Prefab, entry.Portrait);
                strikerById[striker.Id] = striker;
                allStrikers.Add(striker);
            }

            defaultStriker = allStrikers[0];
            isInitialized = true;
        }
    }
}
