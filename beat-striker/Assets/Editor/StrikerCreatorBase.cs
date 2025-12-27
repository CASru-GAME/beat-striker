using UnityEngine;
using UnityEditor;
using System.IO;

namespace BS.Editor {
    /// <summary>
    /// Striker関連のクリエーターの共通処理を提供する基底クラス
    /// </summary>
    public abstract class StrikerCreatorBase {
        protected abstract string TemplateFileName { get; }
        
        protected string CreateFile(string templateFileName, string defaultPrefix, string defaultSuffix) {
            string path = GetSelectedPath();
            
            // 2つ上のフォルダ名を取得
            string folderName = GetTwoLevelsUpFolderName(path);
            
            string fileName = $"{defaultPrefix}{folderName}{defaultSuffix}.cs";
            string fullPath = Path.Combine(path, fileName);
            
            // ファイル名が既に存在する場合は番号を付ける
            int counter = 1;
            while (File.Exists(fullPath)) {
                fileName = $"{defaultPrefix}{folderName}{defaultSuffix}{counter}.cs";
                fullPath = Path.Combine(path, fileName);
                counter++;
            }
            
            string className = Path.GetFileNameWithoutExtension(fileName).Replace(defaultPrefix, "").Replace(defaultSuffix, "");
            string namespaceName = $"Core.{folderName}";
            string template = BuildTemplate(templateFileName, className, namespaceName);
            
            File.WriteAllText(fullPath, template);
            AssetDatabase.Refresh();
            
            // 作成したファイルを選択してリネーム状態にする
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(GetRelativePath(fullPath));
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            
            return fullPath;
        }
        
        protected static string GetSelectedPath() {
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
        
        protected static string GetRelativePath(string absolutePath) {
            if (absolutePath.StartsWith(Application.dataPath)) {
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            return absolutePath;
        }
        
        protected static string GetTwoLevelsUpFolderName(string path) {
            // 2つ上のフォルダ名を取得
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (dirInfo.Parent != null) {
                return dirInfo.Parent.Name;
            }
            // フォールバック: 現在のフォルダ名を使用
            return dirInfo.Name;
        }
        
        protected static string BuildTemplate(string templateFileName, string className, string namespaceName) {
            const string CLASS_PLACEHOLDER = "##CLASS_NAME##";
            const string NAMESPACE_PLACEHOLDER = "##NAMESPACE##";
            
            string template = TryLoadTemplateFromFile(templateFileName);
            if (string.IsNullOrEmpty(template)) {
                Debug.LogWarning($"[StrikerCreator] Template file '{templateFileName}' not found. Using fallback template.");
                return string.Empty;
            }
            return template
                .Replace(CLASS_PLACEHOLDER, className)
                .Replace(NAMESPACE_PLACEHOLDER, namespaceName);
        }

        protected static string TryLoadTemplateFromFile(string templateFileName) {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Editor" });
            if (guids == null || guids.Length == 0) {
                return null;
            }

            // エディタフォルダのパスを取得
            string editorPath = null;
            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Editor/")) {
                    editorPath = Path.GetDirectoryName(path);
                    break;
                }
            }

            if (string.IsNullOrEmpty(editorPath)) {
                return null;
            }

            string templatePath = Path.Combine(editorPath, templateFileName);
            if (!File.Exists(templatePath)) {
                return null;
            }

            return File.ReadAllText(templatePath);
        }
    }
}
