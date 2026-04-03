using UnityEngine;
using System.Collections.Generic;

namespace Alice {
    [System.Serializable]
    public class AppTransitionAnimationEntry {
        public AppScene FromScene;
        public AppScene ToScene;
        public AppTransitionPresenter Prefab;
    }

    public record AppTransitionRequest(AppScene FromScene, AppScene ToScene);

    public interface IAppTransitionFactory {
        IAppTransitionPresenter Create(AppTransitionRequest request);
    }

    public class AppTransitionFactory : MonoBehaviour, IAppTransitionFactory {
        [SerializeField] AppTransitionPresenter defaultTransitionPresenterPrefab;
        [SerializeField] Transform transitionParent;
        [SerializeField] AppTransitionAnimationEntry[] transitionAnimationEntries;

        readonly Dictionary<(AppScene from, AppScene to), AppTransitionPresenter> transitionPresenterMap = new Dictionary<(AppScene from, AppScene to), AppTransitionPresenter>();
        bool isInitialized;

        public IAppTransitionPresenter Create(AppTransitionRequest request) {
            EnsureInitialized();
            if (!transitionPresenterMap.TryGetValue((request.FromScene, request.ToScene), out var selectedTransitionPresenter)) {
                selectedTransitionPresenter = defaultTransitionPresenterPrefab;
            }

            var instance = Object.Instantiate(selectedTransitionPresenter, transitionParent);
            return instance;
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            transitionPresenterMap.Clear();
            foreach (var entry in transitionAnimationEntries) {
                transitionPresenterMap[(entry.FromScene, entry.ToScene)] = entry.Prefab;
            }

            isInitialized = true;
        }
    }
}
