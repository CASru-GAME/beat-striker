using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class FilenameSanitizerMenu
{
    // a-z A-Z 0-9 - _ . 以外の文字を削除（空白も削除）
    private static readonly Regex InvalidCharsRegex = new Regex(@"[^a-zA-Z0-9\-_.]+", RegexOptions.Compiled);

    [MenuItem("Assets/Remove Invalid Chars From File Name", false, 2000)]
    private static void RemoveInvalidCharsFromFileNames()
    {
        var selectedObjects = Selection.GetFiltered<Object>(SelectionMode.Assets);
        int renamedCount = 0;

        foreach (var selectedObject in selectedObjects)
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
            {
                continue;
            }

            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            string sanitizedName = InvalidCharsRegex.Replace(fileName, string.Empty);

            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                sanitizedName = "Renamed";
            }

            if (sanitizedName == fileName)
            {
                continue;
            }

            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assetPath) ?? "Assets", sanitizedName + System.IO.Path.GetExtension(assetPath)));
            string uniqueName = System.IO.Path.GetFileNameWithoutExtension(uniquePath);

            string error = AssetDatabase.RenameAsset(assetPath, uniqueName);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"Failed to rename '{assetPath}': {error}");
                continue;
            }

            renamedCount++;
        }

        if (renamedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Remove Invalid Chars From File Name: renamed {renamedCount} asset(s).");
    }

    [MenuItem("Assets/Remove Invalid Chars From File Name", true)]
    private static bool ValidateRemoveInvalidCharsFromFileNames()
    {
        var selectedObjects = Selection.GetFiltered<Object>(SelectionMode.Assets);
        foreach (var selectedObject in selectedObjects)
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (!string.IsNullOrEmpty(assetPath) && !AssetDatabase.IsValidFolder(assetPath))
            {
                return true;
            }
        }

        return false;
    }
}
