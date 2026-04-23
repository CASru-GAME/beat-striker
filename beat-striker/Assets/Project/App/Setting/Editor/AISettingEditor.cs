using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alice.Editor
{
    [CustomEditor(typeof(AISetting))]
    public class AISettingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // デフォルトのインスペクタを表示
            DrawDefaultInspector();

            var setting = (AISetting)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Build Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Build ML Scene", GUILayout.Height(30)))
            {
                PerformBuild();
            }
        }

        private void PerformBuild()
        {
            string scenePath = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("現在開いているシーンが見つかりません。シーンを保存してから実行してください。");
                return;
            }

            serializedObject.Update();
            var buildPathProp = serializedObject.FindProperty("buildPath");
            string fileName = System.IO.Path.GetFileName(buildPathProp?.stringValue ?? "FighterAI.exe");
            if (string.IsNullOrEmpty(fileName)) fileName = "FighterAI.exe";

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
    }
}
