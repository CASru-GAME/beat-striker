using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.SceneTemplate;

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