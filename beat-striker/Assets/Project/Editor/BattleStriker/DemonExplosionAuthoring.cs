using Core.DarkSatan;
using UnityEditor;
using UnityEngine;

namespace Alice.Editor {
    public static class DemonExplosionAuthoring {
        const string Folder = "Assets/Project/Strikers/DarkSatan/";
        const string CharacterPath = Folder + "DemonKing.prefab";
        const string ExplosionPath = Folder + "Effects/Bullet/DemonExplosion.prefab";

        [MenuItem("Tools/Strikers/Configure Demon Hit Explosions")]
        public static void Configure() {
            var softEdgeShader = Shader.Find("BeatStriker/DemonExplosionSoftEdge");
            foreach (var name in new[] { "DemonExplosionFire", "DemonExplosionSmoke" }) {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Effects/Bullet/" + name + ".mat");
                material.shader = softEdgeShader;
                EditorUtility.SetDirty(material);
            }
            var explosionRoot = PrefabUtility.LoadPrefabContents(ExplosionPath);
            try {
                foreach (var particle in explosionRoot.GetComponentsInChildren<ParticleSystem>(true)) {
                    var main = particle.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                PrefabUtility.SaveAsPrefabAsset(explosionRoot, ExplosionPath);
            } finally { PrefabUtility.UnloadPrefabContents(explosionRoot); }

            var character = PrefabUtility.LoadPrefabContents(CharacterPath);
            try {
                var child = character.transform.Find("Hit Explosion Player");
                if (child == null) {
                    child = new GameObject("Hit Explosion Player").transform;
                    child.SetParent(character.transform, false);
                    child.gameObject.AddComponent<EffectPlayer>();
                }
                var player = child.GetComponent<EffectPlayer>();
                var playerSettings = new SerializedObject(player);
                playerSettings.FindProperty("effectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);
                playerSettings.ApplyModifiedPropertiesWithoutUndo();
                foreach (var state in character.GetComponentsInChildren<DemonMeleeAttackState>(true)) Assign(state, player, .45f);
                foreach (var state in character.GetComponentsInChildren<DemonDirectionalAttackState>(true)) Assign(state, player, .45f);
                foreach (var state in character.GetComponentsInChildren<Core.DarkSatan.SpecialState>(true)) {
                    Assign(state, player, 1.7f);
                    var settings = new SerializedObject(state);
                    settings.FindProperty("explosionSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "Effects/Bullet/DemonHeavyExplosion.wav");
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(character, CharacterPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Demon hit explosions configured.");
            } finally { PrefabUtility.UnloadPrefabContents(character); }
        }

        static void Assign(Component state, EffectPlayer player, float scale) {
            var settings = new SerializedObject(state);
            settings.FindProperty("explosionEffect").objectReferenceValue = player;
            settings.FindProperty("explosionScale").floatValue = scale;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
