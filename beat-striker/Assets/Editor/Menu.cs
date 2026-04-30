using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.SceneTemplate;
using UnityEngine;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement; // プレハブモード判定に必要
using System.Collections.Generic;

public class CustomSceneMenu
{
    // 右クリックメニューの [Create] > [Custom] > [Effect Scene] を作成
    [MenuItem("Assets/Create/⚪️ Effect Scene", false, 2)]
    public static void CreateEffectSceneFromTemplate()
    {
        var template = AssetDatabase.LoadAssetAtPath<SceneTemplateAsset>("Assets/Editor/EffectScene.scenetemplate");
        if (template != null) {
            // 現在選択されているフォルダパスを取得
            string targetFolder = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(targetFolder)) {
                targetFolder = "Assets";
            }

            // ユニークなシーンパスを生成
            string scenePath = GenerateUniqueScenePath(targetFolder, "NewEffectScene");
            
            SceneTemplateService.Instantiate(template, false, scenePath);
        }
    }

    private static string GenerateUniqueScenePath(string folderPath, string baseFileName)
    {
        string scenePath = folderPath + "/" + baseFileName + ".unity";
        int counter = 1;

        while (System.IO.File.Exists(System.IO.Path.GetFullPath(scenePath))) {
            scenePath = folderPath + "/" + baseFileName + counter + ".unity";
            counter++;
        }

        return scenePath;
    }

    private static string GetSelectedFolderPath()
    {
        // 選択されているオブジェクトを取得
        var selected = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);
        if (selected.Length > 0) {
            string path = AssetDatabase.GetAssetPath(selected[0]);
            if (System.IO.File.Exists(path)) {
                // ファイルの場合は親フォルダを返す
                return System.IO.Path.GetDirectoryName(path);
            } else if (System.IO.Directory.Exists(path)) {
                // フォルダの場合はそのパスを返す
                return path;
            }
        }
        return "Assets";
    }

    
}
public class FindNullReferencesWindow : EditorWindow {
    private struct IssueInfo {
        public GameObject gameObject;
        public string componentName;
        public string propertyName;
        public string issueType; // "None", "Missing Ref", "Missing Script"
    }

    private List<IssueInfo> results = new List<IssueInfo>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Find None and Missing")]
    public static void ShowWindow() {
        GetWindow<FindNullReferencesWindow>("Find None & Missing");
    }

    private void OnGUI() {
        if (GUILayout.Button("ヒエラルキー内の None と Missing を検索", GUILayout.Height(30))) {
            SearchInHierarchy();
        }

        EditorGUILayout.Space();

        if (results.Count > 0) {
            GUILayout.Label($"検索結果: {results.Count} 件の問題が見つかりました", EditorStyles.boldLabel);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            foreach (var info in results) {
                GUILayout.BeginHorizontal("box");

                // 問題の種類ごとに色を変えて見やすくする
                GUI.color = info.issueType == "None" ? Color.white : new Color(1f, 0.7f, 0.7f);
                GUILayout.Label($"[{info.issueType}]", GUILayout.Width(90));
                GUI.color = Color.white;

                EditorGUILayout.ObjectField(info.gameObject, typeof(GameObject), true, GUILayout.Width(150));
                GUILayout.Label($"{info.componentName}  >  {info.propertyName}", GUILayout.ExpandWidth(true));

                if (GUILayout.Button("選択", GUILayout.Width(50))) {
                    Selection.activeGameObject = info.gameObject;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }
        else {
            GUILayout.Label("None や Missing は見つかりませんでした。");
        }
    }

    private void SearchInHierarchy() {
        results.Clear();
        List<GameObject> rootObjects = new List<GameObject>();

        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null) {
            rootObjects.Add(prefabStage.prefabContentsRoot);
        }
        else {
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) {
                    rootObjects.AddRange(scene.GetRootGameObjects());
                }
            }
        }

        foreach (GameObject root in rootObjects) {
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allTransforms) {
                GameObject obj = t.gameObject;
                Component[] components = obj.GetComponents<Component>();

                foreach (Component component in components) {
                    // 【種類3】スクリプト自体がMissingになっている場合
                    if (component == null) {
                        results.Add(new IssueInfo {
                            gameObject = obj,
                            componentName = "Unknown Component",
                            propertyName = "-",
                            issueType = "Missing Script"
                        });
                        continue;
                    }

                    // Unity標準コンポーネントは除外（自作スクリプトのみ対象）
                    if (!(component is MonoBehaviour)) continue;

                    SerializedObject so = new SerializedObject(component);
                    SerializedProperty sp = so.GetIterator();

                    while (sp.NextVisible(true)) {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference) {
                            if (sp.name == "m_Script") continue; // システム上の m_Script は除外

                            // 参照が null になっているものを検知
                            if (sp.objectReferenceValue == null) {
                                // InstanceIDが 0 なら「None」、0以外なら「Missing（元々あったが消えた）」
                                bool isPureNone = (sp.objectReferenceInstanceIDValue == 0);

                                results.Add(new IssueInfo {
                                    gameObject = obj,
                                    componentName = component.GetType().Name,
                                    propertyName = sp.displayName,
                                    issueType = isPureNone ? "None" : "Missing Ref"
                                });
                            }
                        }
                    }
                }
            }
        }
    }
}