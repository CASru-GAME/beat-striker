using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Alice.Editor {
    public static class DemonDeathAnimationAuthoring {
        const string PrefabPath = "Assets/Project/Strikers/DarkSatan/DemonKing.prefab";
        const string ClipPath = "Assets/Project/Strikers/DarkSatan/States/DemonDeath.anim";

        [MenuItem("Tools/Strikers/Preview Demon Death Animation")]
        public static void Preview() {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var graph = PlayableGraph.Create("Demon death preview");
            var cameraObject = new GameObject("Death preview camera");
            var lightObject = new GameObject("Death preview light");
            var target = new RenderTexture(480, 480, 24);
            var sheet = new Texture2D(1920, 480, TextureFormat.RGB24, false);
            try {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, root.scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = root.scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .09f, .12f);
                camera.orthographic = true;
                camera.orthographicSize = 1.5f;
                camera.transform.position = new Vector3(4f, 2f, -5f);
                camera.transform.LookAt(new Vector3(0f, 1f, 0f));
                camera.targetTexture = target;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.6f;
                light.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
                var animator = root.transform.Find("DemonKing Visual").GetComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                graph.Play();
                var previous = RenderTexture.active;
                var renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>();
                var baked = renderers.Select(renderer => {
                    var obj = new GameObject("Baked preview");
                    obj.transform.SetParent(renderer.transform, false);
                    var mesh = new Mesh();
                    obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                    obj.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    renderer.enabled = false;
                    return mesh;
                }).ToArray();
                for (int i = 0; i < 4; i++) {
                    playable.SetTime(new[] { 0f, .45f, .8f, 1.4f }[i]);
                    graph.Evaluate(1f / 60f);
                    for (int j = 0; j < renderers.Length; j++) renderers[j].BakeMesh(baked[j]);
                    Debug.Log($"Death pose {i}: humanoid={clip.humanMotion}, hips={animator.GetBoneTransform(HumanBodyBones.Hips).position}");
                    camera.Render();
                    RenderTexture.active = target;
                    sheet.ReadPixels(new Rect(0, 0, 480, 480), i * 480, 0);
                }
                foreach (var mesh in baked) UnityEngine.Object.DestroyImmediate(mesh);
                RenderTexture.active = previous;
                sheet.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DemonDeathPreview.png"), sheet.EncodeToPNG());
                Debug.Log("Demon death preview rendered at 0, 0.45, 0.8 and 1.4 seconds.");
            } finally {
                graph.Destroy();
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(sheet);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/Strikers/Generate Demon Death Animation")]
        public static void Generate() {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var graph = PlayableGraph.Create("Demon death authoring");
            try {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = root.transform.Find("DemonKing Visual").GetComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var idle = root.GetComponentInChildren<Core.DarkSatan.IdleState>(true);
                var idleClip = (AnimationClip)new SerializedObject(idle).FindProperty("animationClip.clip").objectReferenceValue;
                var playable = AnimationClipPlayable.Create(graph, idleClip);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                graph.Play();
                playable.SetTime(.2f);
                graph.Evaluate(1f / 60f);
                using var handler = new HumanPoseHandler(animator.avatar, animator.transform);
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                var initialMuscles = (float[])pose.muscles.Clone();
                var initialPosition = pose.bodyPosition;
                var initialRotation = pose.bodyRotation;
                var curves = Enumerable.Range(0, HumanTrait.MuscleCount + 7).Select(_ => new AnimationCurve()).ToArray();
                var mesh = new Mesh();
                try {
                    for (int frame = 0; frame <= 90; frame++) {
                        float t = frame / 60f;
                        float buckle = Smooth(.12f, .5f, t);
                        float fall = Smooth(.4f, 1.05f, t);
                        float settle = Smooth(1.05f, 1.3f, t);
                        Array.Copy(initialMuscles, pose.muscles, initialMuscles.Length);
                        Set(pose, "Spine Front-Back", -.22f * buckle);
                        Set(pose, "Chest Front-Back", -.18f * buckle);
                        Set(pose, "Head Nod Down-Up", -.28f * buckle);
                        foreach (var side in new[] { "Left", "Right" }) {
                            Set(pose, side + " Upper Leg Front-Back", .48f * buckle * (1f - fall));
                            Set(pose, side + " Lower Leg Stretch", -.65f * buckle * (1f - fall) - .1f * fall);
                            Set(pose, side + " Arm Down-Up", -.4f + .16f * fall);
                            Set(pose, side + " Arm Front-Back", -.12f * buckle + .3f * fall);
                            Set(pose, side + " Forearm Stretch", -.25f * buckle + .15f * settle);
                        }
                        pose.bodyRotation = initialRotation * Quaternion.Euler(-88f * fall, 0f, 12f * fall);
                        pose.bodyPosition = initialPosition + new Vector3(0f, -.25f * buckle - .4f * fall, -.3f * fall);
                        handler.SetHumanPose(ref pose);
                        float minimumY = float.PositiveInfinity;
                        foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                            renderer.BakeMesh(mesh);
                            foreach (var vertex in mesh.vertices) minimumY = Mathf.Min(minimumY, renderer.transform.TransformPoint(vertex).y);
                        }
                        pose.bodyPosition.y += (.015f - minimumY) / animator.humanScale;
                        handler.SetHumanPose(ref pose);
                        for (int i = 0; i < initialMuscles.Length; i++) curves[i].AddKey(t, pose.muscles[i]);
                        var position = pose.bodyPosition;
                        var rotation = pose.bodyRotation;
                        float[] values = { position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w };
                        for (int i = 0; i < 7; i++) curves[initialMuscles.Length + i].AddKey(t, values[i]);
                    }
                } finally { UnityEngine.Object.DestroyImmediate(mesh); }
                var clip = new AnimationClip { name = "DemonDeath", frameRate = 60f, wrapMode = WrapMode.ClampForever };
                string[] rootNames = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
                for (int i = 0; i < curves.Length; i++) {
                    string property = i < HumanTrait.MuscleCount ? HumanTrait.MuscleName[i] : rootNames[i - HumanTrait.MuscleCount];
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curves[i]);
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true;
                settings.loopBlendOrientation = true;
                settings.loopBlendPositionY = true;
                settings.loopBlendPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                if (existing == null) AssetDatabase.CreateAsset(clip, ClipPath);
                else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); }
                AssetDatabase.SaveAssets();
                Debug.Log("DemonDeath authored: 1.5 seconds, 60 fps, grounded humanoid collapse, non-looping.");
            } finally {
                graph.Destroy();
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static float Smooth(float start, float end, float time) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));
        static void Set(HumanPose pose, string name, float value) {
            int index = Array.IndexOf(HumanTrait.MuscleName, name);
            if (index < 0) throw new InvalidOperationException("Unknown humanoid muscle: " + name);
            pose.muscles[index] = value;
        }
    }
}
