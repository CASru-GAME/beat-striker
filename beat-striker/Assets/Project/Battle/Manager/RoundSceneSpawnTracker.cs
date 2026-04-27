using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public sealed class RoundSceneSpawnTracker {
        const string LOG_PREFIX = "[RoundSceneSpawnTracker]";

        readonly HashSet<int> baselineObjectInstanceIds = new();
        readonly List<GameObject> spawnedRootObjects = new();
        bool hasBaseline;

        public void CaptureBaseline() {
            baselineObjectInstanceIds.Clear();
            var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < sceneObjects.Length; i++) {
                var gameObject = sceneObjects[i];
                if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) {
                    continue;
                }

                if ((gameObject.hideFlags & HideFlags.DontSave) != 0) {
                    continue;
                }

                baselineObjectInstanceIds.Add(gameObject.GetInstanceID());
            }

            hasBaseline = true;
            Debug.Log($"{LOG_PREFIX} CaptureBaseline completed. objectCount={baselineObjectInstanceIds.Count}");
        }

        public void DestroySpawnedObjects() {
            if (!hasBaseline) {
                return;
            }

            spawnedRootObjects.Clear();
            var spawnedRootIds = new HashSet<int>();
            var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < sceneObjects.Length; i++) {
                var gameObject = sceneObjects[i];
                if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) {
                    continue;
                }

                if ((gameObject.hideFlags & HideFlags.DontSave) != 0) {
                    continue;
                }

                if (baselineObjectInstanceIds.Contains(gameObject.GetInstanceID())) {
                    continue;
                }

                var root = gameObject.transform.root.gameObject;
                var rootId = root.GetInstanceID();
                if (baselineObjectInstanceIds.Contains(rootId)) {
                    continue;
                }

                if (!spawnedRootIds.Add(rootId)) {
                    continue;
                }

                spawnedRootObjects.Add(root);
            }

            for (var i = 0; i < spawnedRootObjects.Count; i++) {
                Object.Destroy(spawnedRootObjects[i]);
            }

            Debug.Log($"{LOG_PREFIX} DestroySpawnedObjects completed. destroyedCount={spawnedRootObjects.Count}");
            spawnedRootObjects.Clear();
            hasBaseline = false;
            baselineObjectInstanceIds.Clear();
        }
    }
}
