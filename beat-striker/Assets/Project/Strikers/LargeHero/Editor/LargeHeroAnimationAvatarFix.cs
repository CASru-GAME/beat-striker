using UnityEditor;
using UnityEngine;

namespace Core.LargeHero.Editor
{
    /// <summary>
    /// アニメーションFBXのアバターをbighero.fbxのアバターに統一するツール。
    /// Humanoidリターゲット時の体ひねり・腕動作のズレを修正する。
    /// </summary>
    public static class LargeHeroAnimationAvatarFix
    {
        const string BaseFbxGuid      = "6beac3dd0be3810419f915348c66c2f0"; // bighero.fbx
        const string ComboFbxGuid     = "32186077b6f9ba148a13c8d766968580"; // bighero 1 (1) (1).fbx
        const string AirAttackFbxGuid = "b6c6638784309bf44a94603820e6c90a"; // bighero 1 (2).fbx

        [MenuItem("Tools/LargeHero/Fix Animation Avatar (Combo + Air Attack)")]
        static void FixAllAnimationAvatars()
        {
            // bighero.fbxのAvatarアセットを取得
            string basePath = AssetDatabase.GUIDToAssetPath(BaseFbxGuid);
            var sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(basePath);

            if (sourceAvatar == null)
            {
                Debug.LogError($"[LargeHeroAvatarFix] bighero.fbx のAvatarが見つかりません: {basePath}");
                return;
            }

            FixFbx(ComboFbxGuid,     sourceAvatar, "コンボ(bighero 1 (1) (1).fbx)");
            FixFbx(AirAttackFbxGuid, sourceAvatar, "空中攻撃(bighero 1 (2).fbx)");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LargeHeroAvatarFix] 完了。Unityが両FBXを再インポートします。");
        }

        static void FixFbx(string guid, Avatar sourceAvatar, string label)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[LargeHeroAvatarFix] GUIDに対応するアセットが見つかりません: {guid} ({label})");
                return;
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[LargeHeroAvatarFix] ModelImporterが取得できません: {path}");
                return;
            }

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                Debug.LogWarning($"[LargeHeroAvatarFix] {label} はHumanoidではありません。スキップ。");
                return;
            }

            importer.sourceAvatar = sourceAvatar;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[LargeHeroAvatarFix] {label} のアバターを修正しました。");
        }
    }
}
