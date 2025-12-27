using UnityEngine;
using UnityEditor;
using System.IO;

namespace BS.Editor {
    public static class StrikerStateCreator {
        private const string TEMPLATE_FILE_NAME = "StrikerStateTemplate.cs.txt";
        private const string CLASS_PLACEHOLDER = "##CLASS_NAME##";
        private const string NAMESPACE_PLACEHOLDER = "##NAMESPACE##";

        [MenuItem("Assets/Create/🔴 Striker State", false, 1)]
        private static void CreateStrikerState() {
            string path = GetSelectedPath();
            
            // 2つ上のフォルダ名を取得
            string folderName = GetTwoLevelsUpFolderName(path);
            
            string fileName = $"New{folderName}State.cs";
            string fullPath = Path.Combine(path, fileName);
            
            // ファイル名が既に存在する場合は番号を付ける
            int counter = 1;
            while (File.Exists(fullPath)) {
                fileName = $"New{folderName}State{counter}.cs";
                fullPath = Path.Combine(path, fileName);
                counter++;
            }
            
            string className = Path.GetFileNameWithoutExtension(fileName).Replace("New", "").Replace("State", "");
            string namespaceName = $"Core.{folderName}";
            string template = BuildTemplate(className, namespaceName);
            
            File.WriteAllText(fullPath, template);
            AssetDatabase.Refresh();
            
            // 作成したファイルを選択してリネーム状態にする
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(GetRelativePath(fullPath));
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        
        private static string GetSelectedPath() {
            string path = "Assets";
            
            foreach (Object obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets)) {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                    path = Path.GetDirectoryName(path);
                    break;
                }
            }
            
            return path;
        }
        
        private static string GetRelativePath(string absolutePath) {
            if (absolutePath.StartsWith(Application.dataPath)) {
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            return absolutePath;
        }
        
        private static string GetTwoLevelsUpFolderName(string path) {
            // 2つ上のフォルダ名を取得
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (dirInfo.Parent != null) {
                return dirInfo.Parent.Name;
            }
            // フォールバック: 現在のフォルダ名を使用
            return dirInfo.Name;
        }
        
        private static string BuildTemplate(string className, string namespaceName) {
            string template = TryLoadTemplateFromFile();
            if (string.IsNullOrEmpty(template)) {
                Debug.LogWarning($"[{nameof(StrikerStateCreator)}] Template file not found. Using fallback template.");
            }
            return template
                .Replace(CLASS_PLACEHOLDER, className)
                .Replace(NAMESPACE_PLACEHOLDER, namespaceName);
        }

        private static string TryLoadTemplateFromFile() {
            string[] guids = AssetDatabase.FindAssets("StrikerStateCreator t:MonoScript");
            if (guids == null || guids.Length == 0) {
                return null;
            }

            string creatorPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (string.IsNullOrEmpty(creatorPath)) {
                return null;
            }

            string directory = Path.GetDirectoryName(creatorPath);
            string templatePath = Path.Combine(directory, TEMPLATE_FILE_NAME);
            if (!File.Exists(templatePath)) {
                return null;
            }

            return File.ReadAllText(templatePath);
        }

    }
}
