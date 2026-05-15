#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class RemoveUIButtonMenu {
    [MenuItem("GameObject/UI/Button - TextMeshPro", true)]
    private static bool HideButtonValidate() {
        return false;
    }

    [MenuItem("GameObject/UI/Legacy/Text", true)]
    private static bool A() {
        return false;
    }

    [MenuItem("GameObject/UI/Legacy/Button", true)]
    private static bool B() {
        return false;
    }
    [MenuItem("GameObject/UI/Legacy/Dropdown", true)]
    private static bool C() {
        return false;
    }
    [MenuItem("GameObject/UI/Legacy/Input Field", true)]
    private static bool D() {
        return false;
    }

    [MenuItem("GameObject/2D Object/Pixel Perfect Camera (URP)", true)]
    private static bool Ddfa() {
        return false;
    }

    [MenuItem("GameObject/UI/Button", false, 10)]
    static void CreateCustomButton(MenuCommand menuCommand)
    {
        string prefabPath = "Assets/Editor/Button.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            return;
        }

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.name = prefab.name;
        GameObjectUtility.SetParentAndAlign(instance, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(instance, "Create " + instance.name);
        Selection.activeObject = instance;
    }

    [MenuItem("GameObject/🟠 Effect Player", false, 20)]
    static void CreateEffectPlayer(MenuCommand menuCommand) {
        var gameObject = new GameObject("EffectPlayer");

        const string effectPlayerScriptPath = "Assets/Project/Alice/Striker/Components/EffectPlayer.cs";
        var effectPlayerScript = AssetDatabase.LoadAssetAtPath<MonoScript>(effectPlayerScriptPath);
        var effectPlayerType = effectPlayerScript != null ? effectPlayerScript.GetClass() : null;

        if (effectPlayerType == null || !typeof(Component).IsAssignableFrom(effectPlayerType)) {
            UnityEngine.Object.DestroyImmediate(gameObject);
            Debug.LogError("EffectPlayer script type could not be resolved.");
            return;
        }

        gameObject.AddComponent(effectPlayerType);

        GameObjectUtility.SetParentAndAlign(gameObject, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
        Selection.activeObject = gameObject;
    }

}

public class VRMTextureCompressor {
    // --- 右クリックメニューの追加 ---

    [MenuItem("Assets/VRM Utils/Texture Max Size to 128")]
    private static void ResizeTo128() => ResizeSelectedTextures(128);

    [MenuItem("Assets/VRM Utils/Texture Max Size to 256")]
    private static void ResizeTo256() => ResizeSelectedTextures(256);

    [MenuItem("Assets/VRM Utils/Texture Max Size to 512")]
    private static void ResizeTo512() => ResizeSelectedTextures(512);

    // --- 処理本体 ---
    private static void ResizeSelectedTextures(int maxSize) {
        // 選択されたアセットの中からテクスチャのみを抽出（フォルダ選択にも対応）
        UnityEngine.Object[] textures = Selection.GetFiltered(typeof(Texture2D), SelectionMode.DeepAssets);

        if (textures.Length == 0) {
            Debug.LogWarning("対象となるテクスチャファイルが見つかりませんでした。抽出済みの画像ファイルを選択してください。");
            return;
        }

        int count = 0;

        foreach (UnityEngine.Object tex in textures) {
            string path = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null) {
                // UnityのmaxTextureSizeは「長辺」を基準に制限をかけます（アスペクト比は維持されます）
                importer.maxTextureSize = maxSize;

                // 圧縮設定も強制的にオン（モバイル・Web向けに最適化）
                importer.textureCompression = TextureImporterCompression.Compressed;

                // 設定を適用して再インポート
                importer.SaveAndReimport();
                count++;
            }
        }

        Debug.Log($"完了：{count}枚のテクスチャを最大 {maxSize}px (長辺基準) に圧縮しました。");
    }
}


public static class AddressablesFix
{
    [MenuItem("Tools/Addressables - Enable UWR for Local")]
    public static void EnableUWR()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressables Settingsが見つかりません");
            return;
        }

        var updatedCount = 0;
        foreach (var group in settings.groups)
        {
            if (group == null || !group.Name.Contains("Local")) continue;

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) continue;

            schema.UseUnityWebRequestForLocalBundles = true;
            EditorUtility.SetDirty(schema);
            EditorUtility.SetDirty(group);

            updatedCount++;
            Debug.Log($"✅ {group.Name} の UseUnityWebRequestForLocalBundles を ON にしました");
        }

        AssetDatabase.SaveAssets();
        Debug.Log(updatedCount > 0
            ? "完了！ AddressablesをRebuildしてください"
            : "対象のLocalグループが見つかりませんでした。");
    }
}

#endif
