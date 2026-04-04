using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public enum Stage {
        Live,
        Street,
    }

    [System.Serializable]
    public class AppStageEntry {
        public string DisplayName;
        public Stage Stage;
        public string SceneName;
    }

    public record StageInfo(Stage Stage, string DisplayName, string SceneName);

    public interface IStageRegistry {
        StageInfo Default { get; }
        StageInfo GetByStage(Stage stage);
        IReadOnlyList<StageInfo> GetAll();
    }

    public class StageRegistry : MonoBehaviour, IStageRegistry {
        [SerializeField] AppStageEntry[] stageEntries;

        readonly Dictionary<Stage, StageInfo> stageByStage = new Dictionary<Stage, StageInfo>();
        readonly List<StageInfo> allStages = new List<StageInfo>();

        bool isInitialized;
        StageInfo defaultStage;

        public StageInfo Default {
            get {
                EnsureInitialized();
                return defaultStage;
            }
        }

        public StageInfo GetByStage(Stage stage) {
            EnsureInitialized();
            return stageByStage[stage];
        }

        public IReadOnlyList<StageInfo> GetAll() {
            EnsureInitialized();
            return allStages;
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            stageByStage.Clear();
            allStages.Clear();
            foreach (var entry in stageEntries) {
                var stageInfo = new StageInfo(entry.Stage, entry.DisplayName, entry.SceneName);
                stageByStage[stageInfo.Stage] = stageInfo;
                allStages.Add(stageInfo);
            }

            defaultStage = allStages[0];
            isInitialized = true;
        }
    }
}
