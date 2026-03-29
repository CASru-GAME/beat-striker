using UnityEditor;
using UnityEngine;

public static class BatchAnimationSettings
{
    [MenuItem("Tools/Animation/Set Root Transform Bake Into Pose For Selected FBX")]
    private static void SetRootTransformBakeIntoPose()
    {
        var changedCount = 0;

        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                continue;
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

        Debug.Log($"Root Transform設定を更新しました。対象FBX数: {changedCount}");
    }
}
