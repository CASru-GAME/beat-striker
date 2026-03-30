using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class BatchAnimationSettings
{
    [MenuItem("Assets/FBXをHumanoidに変換してRoot Transformを設定", false, 2000)]
    private static void ConvertToHumanoidAndSetRootTransform()
    {
        var changedCount = 0;
        var humanoidChangedCount = 0;
        var newlyImportedClipCount = 0;

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

            var defaultClips = importer.defaultClipAnimations;
            var existingClips = importer.clipAnimations;

            var clips = defaultClips;
            if (defaultClips != null && defaultClips.Length > 0)
            {
                var existingByName = new Dictionary<string, ModelImporterClipAnimation>(System.StringComparer.Ordinal);
                if (existingClips != null)
                {
                    for (var i = 0; i < existingClips.Length; i++)
                    {
                        var existingClip = existingClips[i];
                        if (string.IsNullOrEmpty(existingClip.name))
                        {
                            continue;
                        }

                        existingByName[existingClip.name] = existingClip;
                    }
                }

                clips = new ModelImporterClipAnimation[defaultClips.Length];
                for (var i = 0; i < defaultClips.Length; i++)
                {
                    var defaultClip = defaultClips[i];
                    clips[i] = !string.IsNullOrEmpty(defaultClip.name) && existingByName.TryGetValue(defaultClip.name, out var existingClip)
                        ? existingClip
                        : defaultClip;
                }
            }

            var hasClips = clips != null && clips.Length > 0;
            var importedNewClips = hasClips && (existingClips == null || existingClips.Length < clips.Length);
            if (importedNewClips)
            {
                newlyImportedClipCount += clips.Length - (existingClips?.Length ?? 0);
            }

            var updated = false;
            if (hasClips)
            {
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
            }

            if (!updated && !importedNewClips && importer.animationType == ModelImporterAnimationType.Human)
            {
                continue;
            }

            if (hasClips)
            {
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
            changedCount++;
        }

        Debug.Log($"Humanoid変換数: {humanoidChangedCount}, Root Transform設定更新数: {changedCount}, 新規クリップ読込数: {newlyImportedClipCount}");
    }

    [MenuItem("Assets/FBXをHumanoidに変換してRoot Transformを設定", true)]
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
