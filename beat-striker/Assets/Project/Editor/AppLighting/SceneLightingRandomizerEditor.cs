using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alice.Editor {
    [CustomEditor(typeof(SceneLightingRandomizer))]
    public class SceneLightingRandomizerEditor : UnityEditor.Editor {
        const string PresetNamePrefix = "Captured Lighting";

        SerializedProperty presetsProp;

        // ReSharper disable once UnusedMember.Local
        void OnEnable() {
            presetsProp = serializedObject.FindProperty("presets");
        }

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);

            if (GUILayout.Button("Capture Current Scene Lighting", GUILayout.Height(30))) {
                CaptureCurrentSceneLighting();
            }

            DrawApplyPresetButtons();
        }

        void CaptureCurrentSceneLighting() {
            var randomizer = (SceneLightingRandomizer)target;
            var presetName = $"{PresetNamePrefix} {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            Undo.RecordObject(randomizer, "Capture Current Scene Lighting");
            randomizer.AddCurrentSceneLightingAsPreset(presetName);
            EditorUtility.SetDirty(randomizer);

            if (randomizer.gameObject.scene.IsValid()) {
                EditorSceneManager.MarkSceneDirty(randomizer.gameObject.scene);
            }
        }

        void DrawApplyPresetButtons() {
            if (presetsProp == null || presetsProp.arraySize == 0) {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apply Saved Preset", EditorStyles.boldLabel);

            for (var i = 0; i < presetsProp.arraySize; i++) {
                var presetProp = presetsProp.GetArrayElementAtIndex(i);
                var nameProp = presetProp.FindPropertyRelative("name");
                var presetName = string.IsNullOrEmpty(nameProp.stringValue) ? $"Preset {i}" : nameProp.stringValue;

                if (GUILayout.Button($"Apply {presetName}", GUILayout.Height(24))) {
                    ApplyPreset(i);
                }
            }
        }

        void ApplyPreset(int presetIndex) {
            var randomizer = (SceneLightingRandomizer)target;

            randomizer.ApplyPreset(presetIndex);
            EditorUtility.SetDirty(randomizer);

            if (randomizer.gameObject.scene.IsValid()) {
                EditorSceneManager.MarkSceneDirty(randomizer.gameObject.scene);
            }
        }
    }
}
