using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Alice.Editor {
    [CustomEditor(typeof(MusicRegistry))]
    [CanEditMultipleObjects]
    public class MusicRegistryEditor : UnityEditor.Editor {
        static readonly FieldInfo MusicEntriesField =
            typeof(MusicRegistry).GetField("musicEntries", BindingFlags.NonPublic | BindingFlags.Instance);

        public override void OnInspectorGUI() {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Build Actions", EditorStyles.boldLabel);
            if (GUILayout.Button("Recalculate BPM / Length", GUILayout.Height(30))) {
                PerformRecalculateAll();
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void PerformRecalculateAll() {
            var registries = CollectAllRegistries();
            if (registries.Count == 0) {
                Debug.LogWarning("[MusicRegistryEditor] No MusicRegistry found in open scenes or prefabs.");
                return;
            }

            var recalculatedRegistryCount = 0;
            foreach (var registry in registries) {
                Recalculate(registry);
                recalculatedRegistryCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MusicRegistryEditor] Recalculated all registries. count={recalculatedRegistryCount}");
        }

        static List<MusicRegistry> CollectAllRegistries() {
            var allRegistries = new List<MusicRegistry>();
            var seen = new HashSet<MusicRegistry>();

            // Include registries in currently opened scenes.
#if UNITY_2023_1_OR_NEWER
            foreach (var registry in Object.FindObjectsByType<MusicRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
#else
            foreach (var registry in Object.FindObjectsOfType<MusicRegistry>(true)) {
#endif
                if (registry != null && seen.Add(registry)) {
                    allRegistries.Add(registry);
                }
            }

            // Include registries stored in prefab assets.
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids) {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefabRoot == null) {
                    continue;
                }

                var registriesInPrefab = prefabRoot.GetComponentsInChildren<MusicRegistry>(true);
                foreach (var registry in registriesInPrefab) {
                    if (registry != null && seen.Add(registry)) {
                        allRegistries.Add(registry);
                    }
                }
            }

            return allRegistries;
        }

        static void Recalculate(MusicRegistry registry) {
            if (MusicEntriesField == null) {
                Debug.LogError("[MusicRegistryEditor] Failed to find 'musicEntries' field.");
                return;
            }

            var entries = MusicEntriesField.GetValue(registry) as AppMusicEntry[];
            if (entries == null) {
                Debug.LogWarning("[MusicRegistryEditor] musicEntries is null.");
                return;
            }

            Undo.RecordObject(registry, "Recalculate Music Metadata");
            var updatedCount = 0;
            foreach (var entry in entries) {
                if (entry == null) {
                    continue;
                }

                var previewClip = LoadAddressableAsset(entry.PreviewAudioClipReference);
                var mainClip = LoadAddressableAsset(entry.AudioClipReference);
                var beatData = entry.BeatData;

                var clipForLength = mainClip != null ? mainClip : previewClip;
                entry.PrecomputedLengthSeconds = clipForLength != null ? clipForLength.length : 0f;
                entry.PrecomputedBpm = BeatDataParser.CalculateBpm(beatData);
                updatedCount++;
            }

            EditorUtility.SetDirty(registry);
            Debug.Log($"[MusicRegistryEditor] Recalculated metadata for {updatedCount} music entries.");
        }

        static T LoadAddressableAsset<T>(AssetReferenceT<T> assetReference) where T : Object {
            if (assetReference == null || string.IsNullOrEmpty(assetReference.AssetGUID)) {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(assetReference.AssetGUID);
            if (string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }
    }
}
