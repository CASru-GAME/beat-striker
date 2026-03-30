using UnityEditor;
using UnityEngine;

public static class BatchAnimationSettings
{
    [MenuItem("Assets/Animation/FBXをHumanoidに変換してRoot Transformを設定", false, 2000)]
    private static void ConvertToHumanoidAndSetRootTransform()
    {
        var changedCount = 0;
        var humanoidChangedCount = 0;

        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                continue;
            }

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                humanoidChangedCount++;
            }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                continue;
            }

            var updated = false;
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];

                // Root Transform Rotation: Bake Into Pose ON / Based Upon Original
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;

                // Root Transform Position (Y/XZ): Bake Into Pose ON
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;

                if (!string.IsNullOrEmpty(clip.name) && clip.name.Contains("ループ", System.StringComparison.Ordinal))
                {
                    clip.loopTime = true;
                }

                clips[i] = clip;
                updated = true;
            }

            if (!updated)
            {
                continue;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            changedCount++;
        }

        Debug.Log($"Humanoid変換数: {humanoidChangedCount}, Root Transform設定更新数: {changedCount}");
    }

    [MenuItem("Assets/Animation/FBXをHumanoidに変換してRoot Transformを設定", true)]
    private static bool ValidateConvertToHumanoidAndSetRootTransform()
    {
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (AssetImporter.GetAtPath(path) is ModelImporter)
            {
                return true;
            }
        }

        return false;
    }
}
