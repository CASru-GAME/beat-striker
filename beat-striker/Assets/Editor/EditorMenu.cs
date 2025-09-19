#if UNITY_EDITOR
using UnityEditor;
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

        GameObject instance = Object.Instantiate(prefab);
        instance.name = prefab.name;
        GameObjectUtility.SetParentAndAlign(instance, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(instance, "Create " + instance.name);
        Selection.activeObject = instance;
    }

}

#endif
