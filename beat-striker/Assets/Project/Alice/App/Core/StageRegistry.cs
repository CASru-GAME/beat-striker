using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    [System.Serializable]
    public class AppStageEntry {
        public string Id;
        public string DisplayName;
        public string SceneName;
    }

    public record StageInfo(string Id, string DisplayName, string SceneName);

    public interface IStageRegistry {
        StageInfo Default { get; }
        StageInfo GetById(string id);
        IReadOnlyList<StageInfo> GetAll();
    }

    public class StageRegistry : MonoBehaviour, IStageRegistry {
        [SerializeField] AppStageEntry[] stageEntries;

        readonly Dictionary<string, StageInfo> stageById = new Dictionary<string, StageInfo>();
        readonly List<StageInfo> allStages = new List<StageInfo>();

        bool isInitialized;
        StageInfo defaultStage;

        public StageInfo Default {
            get {
                EnsureInitialized();
                return defaultStage;
            }
        }

        public StageInfo GetById(string id) {
            EnsureInitialized();
            return stageById[id];
        }

        public IReadOnlyList<StageInfo> GetAll() {
            EnsureInitialized();
            return allStages;
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            stageById.Clear();
            allStages.Clear();
            foreach (var entry in stageEntries) {
                var stage = new StageInfo(entry.Id, entry.DisplayName, entry.SceneName);
                stageById[stage.Id] = stage;
                allStages.Add(stage);
            }

            defaultStage = allStages[0];
            isInitialized = true;
        }
    }
}
