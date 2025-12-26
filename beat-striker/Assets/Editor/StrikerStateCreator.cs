using UnityEngine;
using UnityEditor;
using System.IO;

namespace BS.Editor {
    public static class StrikerStateCreator {
        [MenuItem("Assets/Create/🔴 Striker State", false, 1)]
        private static void CreateStrikerState() {
            string path = GetSelectedPath();
            string fileName = "NewStrikerState.cs";
            string fullPath = Path.Combine(path, fileName);
            
            // ファイル名が既に存在する場合は番号を付ける
            int counter = 1;
            while (File.Exists(fullPath)) {
                fileName = $"NewStrikerState{counter}.cs";
                fullPath = Path.Combine(path, fileName);
                counter++;
            }
            
            string className = Path.GetFileNameWithoutExtension(fileName);
            string template = GetTemplate(className);
            
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
        
        private static string GetTemplate(string className) {
            return @"using Core.Battle;
using Core.Striker;
using UnityEngine;

public class " + className + @" : StrikerState {
    public override void Enter(IStrikerHub hub) {
        // 状態に入った時の処理
    }

    public override void Exit() {
        // 状態を抜ける時の処理
    }

    public override void OnUpdate(IStrikerHub hub) {
        // 毎フレームの更新処理
    }

    // 必要に応じてオーバーライド
    // public void OnAttackRequested(IStrikerHub hub) { }
    // public void OnChargeRequested(IStrikerHub hub) { }
    // public void OnDashRequested(IStrikerHub hub) { }
    // public void OnGuardRequested(IStrikerHub hub) { }
    // public void OnHit(IStrikerHub hub, HitStatus status) { }
    // public void OnMiss(IStrikerHub hub) { }
}
";
        }
    }
}
