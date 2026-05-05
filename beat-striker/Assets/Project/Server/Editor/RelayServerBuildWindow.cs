using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alice.Editor {
    public sealed class RelayServerBuildWindow : EditorWindow {
        public const string RelayEmptyScenePath = "Assets/Project/Server/RelayServerEmpty.unity";

        /// <summary>
        /// このウィンドウからのプレイヤービルド時のみ付与されるシンボル。
        /// クライアント専用 asmdef の Define Constraints に <c>!ALICE_RELAY_SERVER</c> を書くとリレービルドから除外できる。
        /// </summary>
        public const string RelayServerScriptingDefine = "ALICE_RELAY_SERVER";

        const string PrefOutputParent = "Alice.RelayServerBuild.OutputParent";
        const string PrefTargetIndex = "Alice.RelayServerBuild.TargetIndex";
        const string PrefDedicatedServer = "Alice.RelayServerBuild.DedicatedServer";
        const string PrefMacDedicatedAttempt = "Alice.RelayServerBuild.MacDedicatedAttempt";
        const string PrefWindowsDedicatedAttempt = "Alice.RelayServerBuild.WindowsDedicatedAttempt";
        const string PrefHighManagedStripping = "Alice.RelayServerBuild.HighManagedStripping";

        /// <summary>
        /// UnityEditor.AddressableAssets.AddressablesPreferences と同じキー。
        /// Addressables の「Preferences で決める」モード時、プレイヤービルドにバンドルを載せないために false にする。
        /// </summary>
        const string AddressablesBuildWithPlayerPrefKey = "Addressables.BuildAddressablesWithPlayerBuild";

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
        bool dedicatedServerBuild = true;
        bool windowsDedicatedServerAttempt = true;
        bool macDedicatedServerAttempt;
        bool relayHighManagedStripping = true;

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
            dedicatedServerBuild = EditorPrefs.GetInt(PrefDedicatedServer, 1) != 0;
            windowsDedicatedServerAttempt = EditorPrefs.GetInt(PrefWindowsDedicatedAttempt, 1) != 0;
            macDedicatedServerAttempt = EditorPrefs.GetInt(PrefMacDedicatedAttempt, 0) != 0;
            relayHighManagedStripping = EditorPrefs.GetInt(PrefHighManagedStripping, 1) != 0;
        }

        void OnGUI() {
            EditorGUILayout.HelpBox(
                "Fusion オンライン用リレー（OnlineSessionRelayServer）のスタンドアロンビルドです。\n"
                + "ビルド先 OS は下で選べます（Project の Build Settings を切り替える必要はありません）。\n"
                + "「Dedicated Server」は Standalone の Server サブターゲット（ヘッドレス寄り・グラフィック資産の削減）。"
                + "ビルド時に UNITY_SERVER が定義されます。\n"
                + "※ Windows / macOS は OS ごとの「Dedicated Server を使う」で Server サブターゲットに切り替えます。"
                + "オフのときは Player でビルドします（Hub の Dedicated Server Build Support が無い環境向け）。\n"
                + "含めるシーンは "
                + RelayEmptyScenePath
                + " のみ。シーン上の OnlineSessionRelayServer（Start On Awake）で起動するため、"
                + "生成した実行ファイルをそのまま起動すればよいです。\n"
                + "このビルドでは "
                + RelayServerScriptingDefine
                + " が付く。Alice.Project.Client・VRM10・UniGLTF は asmdef で "
                + "`!" + RelayServerScriptingDefine + "` により除外済み。\n"
                + "サイズ: Unity ランタイム＋Fusion だけで 150〜250MB 超はよくある（ゲーム資産を削ってもエンジン骨格は残る）。\n"
                + "Addressables バンドルはプレイヤーに同梱しない。マネージドストリッピング High で未使用 IL を削る。\n"
                + "それでも大きいときは Build Report で Unity のどのモジュールが支配的か確認するか、リレー専用の別 Unity プロジェクトが最終手段。",
                MessageType.Info);

            relayTargetIndex = EditorGUILayout.Popup("ビルド先 OS", relayTargetIndex, RelayTargetLabels);
            EditorPrefs.SetInt(PrefTargetIndex, relayTargetIndex);

            dedicatedServerBuild = EditorGUILayout.Toggle(
                "Dedicated Server ビルド（細い・ヘッドレス寄り）",
                dedicatedServerBuild);
            EditorPrefs.SetInt(PrefDedicatedServer, dedicatedServerBuild ? 1 : 0);

            var macOsSelected = relayTargetIndex >= 0 && relayTargetIndex < RelayTargets.Length
                && RelayTargets[relayTargetIndex] == BuildTarget.StandaloneOSX;
            var windowsSelected = relayTargetIndex >= 0 && relayTargetIndex < RelayTargets.Length
                && RelayTargets[relayTargetIndex] == BuildTarget.StandaloneWindows64;

            if (windowsSelected) {
                using (new EditorGUI.DisabledScope(!dedicatedServerBuild)) {
                    windowsDedicatedServerAttempt = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Windows: Dedicated Server を使う（Hub に Dedicated Server Build Support 必須）",
                            "オフのときは Windows 向けは Player サブターゲットでビルドします。"),
                        windowsDedicatedServerAttempt);
                }

                EditorPrefs.SetInt(PrefWindowsDedicatedAttempt, windowsDedicatedServerAttempt ? 1 : 0);
            }

            if (macOsSelected) {
                using (new EditorGUI.DisabledScope(!dedicatedServerBuild)) {
                    macDedicatedServerAttempt = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Mac: Dedicated Server を使う（Hub に Mac Dedicated Server モジュール必須）",
                            "オフのときは macOS 向けは自動的に Player サブターゲットでビルドします。"),
                        macDedicatedServerAttempt);
                }

                if (macDedicatedServerAttempt) {
                    EditorPrefs.SetInt(PrefMacDedicatedAttempt, 1);
                }
                else {
                    EditorPrefs.DeleteKey(PrefMacDedicatedAttempt);
                }
            }

            outputParentFolder = EditorGUILayout.TextField("出力親フォルダ", outputParentFolder);
            executableBaseName = EditorGUILayout.TextField("実行ファイル名（拡張子なし）", executableBaseName);
            developmentBuild = EditorGUILayout.Toggle("Development Build", developmentBuild);

            relayHighManagedStripping = EditorGUILayout.Toggle(
                new GUIContent(
                    "ビルド時だけマネージドストリッピング High",
                    "このビルドの間だけ Standalone のストリッピングを High にし、終了後に元に戻す。サイズ削減・ビルド時間増の可能性あり。"),
                relayHighManagedStripping);
            EditorPrefs.SetInt(PrefHighManagedStripping, relayHighManagedStripping ? 1 : 0);

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

            var outDir = outputParentFolder;
            Directory.CreateDirectory(outDir);

            var target = RelayTargets[relayTargetIndex];
            var baseName = executableBaseName.Trim();
            var locationPathName = MakeExecutablePath(outDir, baseName, target);

            var options = BuildOptions.None;
            if (developmentBuild) {
                options |= BuildOptions.Development;
            }

            var subtarget = ResolveStandaloneSubtarget(
                target,
                dedicatedServerBuild,
                windowsDedicatedServerAttempt,
                macDedicatedServerAttempt);

            var previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            var previousAddressablesBuildWithPlayer =
                EditorPrefs.GetBool(AddressablesBuildWithPlayerPrefKey, true);

            var standaloneNamedTarget = NamedBuildTarget.Standalone;
            var previousManagedStripping = PlayerSettings.GetManagedStrippingLevel(standaloneNamedTarget);

            EditorUserBuildSettings.standaloneBuildSubtarget = subtarget;
            EditorPrefs.SetBool(AddressablesBuildWithPlayerPrefKey, false);
            if (relayHighManagedStripping) {
                PlayerSettings.SetManagedStrippingLevel(standaloneNamedTarget, ManagedStrippingLevel.High);
            }

            try {
                var buildPlayerOptions = new BuildPlayerOptions {
                    scenes = scenes,
                    locationPathName = locationPathName,
                    target = target,
                    targetGroup = BuildTargetGroup.Standalone,
                    subtarget = (int)subtarget,
                    options = options,
                    extraScriptingDefines = new[] { RelayServerScriptingDefine },
                };

                Debug.Log(
                    $"[RelayServerBuild] output={locationPathName}, target={target}, subtarget={subtarget}, define={RelayServerScriptingDefine}, "
                    + $"addressablesWithPlayerPref=false (restores after), managedStripping={(relayHighManagedStripping ? "High(temp)" : "unchanged")}, sceneCount={scenes.Length}");

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
            finally {
                EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;
                EditorPrefs.SetBool(AddressablesBuildWithPlayerPrefKey, previousAddressablesBuildWithPlayer);
                PlayerSettings.SetManagedStrippingLevel(standaloneNamedTarget, previousManagedStripping);
            }
        }

        static StandaloneBuildSubtarget ResolveStandaloneSubtarget(
            BuildTarget target,
            bool dedicatedServerBuild,
            bool windowsDedicatedServerAttempt,
            bool macDedicatedServerAttempt) {
            if (!dedicatedServerBuild) {
                return StandaloneBuildSubtarget.Player;
            }

            if (target == BuildTarget.StandaloneWindows64 && !windowsDedicatedServerAttempt) {
                Debug.Log(
                    "[RelayServerBuild] Windows 向けは「Windows: Dedicated Server を使う」がオフのため Player サブターゲットでビルドします。");
                return StandaloneBuildSubtarget.Player;
            }

            if (target == BuildTarget.StandaloneOSX && !macDedicatedServerAttempt) {
                Debug.Log(
                    "[RelayServerBuild] macOS 向けは未チェックのため Player サブターゲットでビルドします"
                    + "（Dedicated Server には Unity Hub の Mac dedicated server 用モジュールが必要）。");
                return StandaloneBuildSubtarget.Player;
            }

            return StandaloneBuildSubtarget.Server;
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
