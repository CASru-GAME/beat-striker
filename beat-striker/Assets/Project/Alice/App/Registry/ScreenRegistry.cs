using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    [System.Serializable]
    public class AppScreenEntry {
        public string DisplayName;
        public AppScene Scene;
        public bool CreateCursor = true;
        public AudioClip Bgm;
        public string SceneName;
    }

    public record ScreenInfo(
        AppScene Scene,
        string DisplayName,
        bool CreateCursor,
        AudioClip Bgm,
        string SceneName);

    public interface IScreenRegistry {
        ScreenInfo Default { get; }
        ScreenInfo GetByScene(AppScene scene);
        ScreenInfo GetBySceneName(string sceneName);
        IReadOnlyList<ScreenInfo> GetAll();
    }

    public class ScreenRegistry : MonoBehaviour, IScreenRegistry {
        [SerializeField] AppScreenEntry[] screenEntries;

        readonly Dictionary<AppScene, ScreenInfo> screenByScene = new();
        readonly Dictionary<string, ScreenInfo> screenBySceneName = new();
        readonly List<ScreenInfo> allScreens = new();

        bool isInitialized;
        ScreenInfo defaultScreen;

        public ScreenInfo Default {
            get {
                EnsureInitialized();
                return defaultScreen;
            }
        }

        public ScreenInfo GetByScene(AppScene scene) {
            EnsureInitialized();
            return screenByScene[scene];
        }

        public ScreenInfo GetBySceneName(string sceneName) {
            EnsureInitialized();
            return screenBySceneName[sceneName];
        }

        public IReadOnlyList<ScreenInfo> GetAll() {
            EnsureInitialized();
            return allScreens;
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            screenByScene.Clear();
            screenBySceneName.Clear();
            allScreens.Clear();
            for (var i = 0; i < screenEntries.Length; i++) {
                var entry = screenEntries[i];
                var screenInfo = new ScreenInfo(
                    entry.Scene,
                    entry.DisplayName,
                    entry.CreateCursor,
                    entry.Bgm,
                    entry.SceneName);
                screenByScene[screenInfo.Scene] = screenInfo;
                screenBySceneName[screenInfo.SceneName] = screenInfo;
                allScreens.Add(screenInfo);
            }

            defaultScreen = allScreens[0];
            isInitialized = true;
        }
    }
}
