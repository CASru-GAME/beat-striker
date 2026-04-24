using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alice.Editor
{
    [CustomEditor(typeof(AISetting))]
    public class AISettingEditor : UnityEditor.Editor
    {
        SerializedProperty modeProp;
        SerializedProperty demonstrationNameProp;
        SerializedProperty learningPlayer1Prop;
        SerializedProperty learningOpponentsProp;
        SerializedProperty testOpponentSequenceProp;
        SerializedProperty emaSmoothingProp;
        SerializedProperty emaFloorScaleProp;
        SerializedProperty buildPathProp;

        void OnEnable() {
            modeProp = serializedObject.FindProperty("mode");
            demonstrationNameProp = serializedObject.FindProperty("demonstrationName");
            learningPlayer1Prop = serializedObject.FindProperty("learningPlayer1");
            learningOpponentsProp = serializedObject.FindProperty("learningOpponents");
            testOpponentSequenceProp = serializedObject.FindProperty("testOpponentSequence");
            emaSmoothingProp = serializedObject.FindProperty("emaSmoothing");
            emaFloorScaleProp = serializedObject.FindProperty("emaFloorScale");
            buildPathProp = serializedObject.FindProperty("buildPath");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(modeProp);
            var mode = (AiPlayMode)modeProp.enumValueIndex;

            if (IsDemonstrationRecordingMode(mode)) {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Demonstration Recording", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(demonstrationNameProp);
                EditorGUILayout.HelpBox("Demonstration Nameには実行時に yyyyMMdd_HHmmss 形式のタイムスタンプが自動付与されます。", MessageType.Info);
            }

            if (UsesAiSettingStrikerSelection(mode)) {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Learning Setup", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(learningPlayer1Prop, true);

                if (UsesLearningOpponentPool(mode)) {
                    EditorGUILayout.PropertyField(learningOpponentsProp, true);
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Opponent Selection - EMA", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(emaSmoothingProp);
                    EditorGUILayout.PropertyField(emaFloorScaleProp);
                }
            }

            if (UsesFixedTestOpponentSequence(mode)) {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Test Opponent Sequence", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(testOpponentSequenceProp, true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Build Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(buildPathProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Build Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Build ML Scene", GUILayout.Height(30)))
            {
                PerformBuild();
            }

            serializedObject.ApplyModifiedProperties();
        }

        static bool IsDemonstrationRecordingMode(AiPlayMode mode) => mode == AiPlayMode.Record;

        static bool UsesAiSettingStrikerSelection(AiPlayMode mode) {
            return mode is AiPlayMode.Record or AiPlayMode.Learning or AiPlayMode.LearningSelfPlay;
        }

        static bool UsesLearningOpponentPool(AiPlayMode mode) {
            return mode is AiPlayMode.Record or AiPlayMode.Learning;
        }

        static bool UsesFixedTestOpponentSequence(AiPlayMode mode) => mode == AiPlayMode.Test;

        private void PerformBuild()
        {
            string scenePath = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("現在開いているシーンが見つかりません。シーンを保存してから実行してください。");
                return;
            }

            serializedObject.Update();
            string fileName = System.IO.Path.GetFileName(buildPathProp?.stringValue ?? "FighterAI.exe");
            if (string.IsNullOrEmpty(fileName)) fileName = "FighterAI.exe";
            int previousMode = modeProp?.enumValueIndex ?? (int)AiPlayMode.Inference;

            // フォルダ構成: Dist/ML-Scene/YYYY-MM-dd-HH-mm-ss/
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            string outputFolder = System.IO.Path.Combine("Dist", "ML-Scene", timestamp);
            string targetPath = System.IO.Path.Combine(outputFolder, fileName);

            // フォルダが存在しない場合は作成
            if (!System.IO.Directory.Exists(outputFolder))
            {
                System.IO.Directory.CreateDirectory(outputFolder);
            }

            Debug.Log($"Building Scene: {scenePath} to {targetPath}");

            string[] scenes = { scenePath };
            
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = targetPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            try
            {
                if (modeProp != null)
                {
                    modeProp.enumValueIndex = (int)AiPlayMode.Learning;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(target);
                }

                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                var summary = report.summary;

                if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    Debug.Log($"Build succeeded: {summary.totalSize} bytes");
                }
                else if (summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
                {
                    Debug.LogError("Build failed");
                }
            }
            finally
            {
                if (modeProp != null)
                {
                    serializedObject.Update();
                    modeProp.enumValueIndex = previousMode;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
}
