using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alice.Editor {
    public sealed class RelayServerBuildWindow : EditorWindow {
        public const string RelayEmptyScenePath = "Assets/Project/Server/RelayServerEmpty.unity";

        const string PrefOutputParent = "Alice.RelayServerBuild.OutputParent";
        const string PrefTargetIndex = "Alice.RelayServerBuild.TargetIndex";

        static readonly BuildTarget[] RelayTargets = {
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneOSX,
            BuildTarget.StandaloneLinux64,
        };

        static readonly string[] RelayTargetLabels = {
            "Windows (64-bit)",
            "macOS",
            "Linux (64-bit)",
        };

        string outputParentFolder = "Dist/RelayServer";
        string executableBaseName = "BeatStrikerRelayServer";
        int relayTargetIndex;
        bool developmentBuild;

        [MenuItem("Alice/Build/Relay Server Build...")]
        static void Open() {
            var window = GetWindow<RelayServerBuildWindow>("Relay Server Build");
            window.minSize = new Vector2(460f, 280f);
            window.LoadPrefs();
        }

        static int DefaultRelayTargetIndex() {
#if UNITY_EDITOR_OSX
            return 1;
#elif UNITY_EDITOR_LINUX
            return 2;
#else
            return 0;
#endif
        }

        void LoadPrefs() {
            var saved = EditorPrefs.GetString(PrefOutputParent, string.Empty);
            if (!string.IsNullOrEmpty(saved)) {
                outputParentFolder = saved;
            }

            relayTargetIndex = EditorPrefs.GetInt(PrefTargetIndex, DefaultRelayTargetIndex());
            relayTargetIndex = Mathf.Clamp(relayTargetIndex, 0, RelayTargets.Length - 1);
        }

        void OnGUI() {
            EditorGUILayout.HelpBox(
                "Fusion オンライン用リレー（OnlineSessionRelayServer）のスタンドアロンビルドです。\n"
                + "ビルド先 OS は下で選べます（Project の Build Settings を切り替える必要はありません）。\n"
                + "含めるシーンは "
                + RelayEmptyScenePath
                + " のみ。シーン上の OnlineSessionRelayServer（Start On Awake）で起動するため、"
                + "生成した実行ファイルをそのまま起動すればよいです。",
                MessageType.Info);

            relayTargetIndex = EditorGUILayout.Popup("ビルド先 OS", relayTargetIndex, RelayTargetLabels);
            EditorPrefs.SetInt(PrefTargetIndex, relayTargetIndex);

            outputParentFolder = EditorGUILayout.TextField("出力親フォルダ", outputParentFolder);
            executableBaseName = EditorGUILayout.TextField("実行ファイル名（拡張子なし）", executableBaseName);
            developmentBuild = EditorGUILayout.Toggle("Development Build", developmentBuild);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(executableBaseName))) {
                if (GUILayout.Button("ビルド実行", GUILayout.Height(32f))) {
                    RunBuild();
                }
            }
        }

        void RunBuild() {
            EditorPrefs.SetString(PrefOutputParent, outputParentFolder);
            EditorPrefs.SetInt(PrefTargetIndex, relayTargetIndex);

            if (!File.Exists(RelayEmptyScenePath)) {
                EditorUtility.DisplayDialog(
                    "Relay Server Build",
                    $"空シーンが見つかりません:\n{RelayEmptyScenePath}",
                    "OK");
                return;
            }

            var scenes = new[] { RelayEmptyScenePath };

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var outDir = Path.Combine(outputParentFolder, timestamp);
            Directory.CreateDirectory(outDir);

            var target = RelayTargets[relayTargetIndex];
            var baseName = executableBaseName.Trim();
            var locationPathName = MakeExecutablePath(outDir, baseName, target);

            var options = BuildOptions.None;
            if (developmentBuild) {
                options |= BuildOptions.Development;
            }

            var buildPlayerOptions = new BuildPlayerOptions {
                scenes = scenes,
                locationPathName = locationPathName,
                target = target,
                options = options,
            };

            Debug.Log($"[RelayServerBuild] output={locationPathName}, target={target}, sceneCount={scenes.Length}");

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded) {
                EditorUtility.DisplayDialog("Relay Server Build", $"成功:\n{locationPathName}", "OK");
                EditorUtility.RevealInFinder(Path.GetFullPath(locationPathName));
            }
            else {
                EditorUtility.DisplayDialog("Relay Server Build", "ビルド失敗。Console を確認してください。", "OK");
            }
        }

        static string MakeExecutablePath(string directory, string baseName, BuildTarget target) {
            switch (target) {
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(directory, baseName + ".exe");
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(directory, baseName + ".app");
                case BuildTarget.StandaloneLinux64:
                    return Path.Combine(directory, baseName);
                default:
                    return Path.Combine(directory, baseName + "_" + target);
            }
        }
    }
}
