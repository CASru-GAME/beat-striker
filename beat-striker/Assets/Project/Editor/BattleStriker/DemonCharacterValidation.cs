using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Alice.Editor {
    public static class DemonCharacterValidation {
        [MenuItem("Tools/Strikers/Validate Demon Character")]
        public static void Validate() {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Strikers/TutorialDemon/TutorialDemon.prefab");
            var root = UnityEngine.Object.Instantiate(prefab);
            try {
                if (root.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                    throw new InvalidOperationException("Missing script in demon hierarchy");
                if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c.GetType().Namespace == "Core.LargeSatan"))
                    throw new InvalidOperationException("Warrior script remains on demon");
                var visual = root.transform.Find("DemonKing Visual").gameObject;
                var animator = visual.GetComponent<Animator>();
                if (animator == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("Invalid humanoid avatar");
                if (animator.humanScale < .5f || animator.humanScale > 3f)
                    throw new InvalidOperationException($"Demon humanoid scale is inconsistent with its model: {animator.humanScale}");
                var poleArm = root.GetComponentInChildren<Core.DarkSatan.PoleArm>(true);
                if (poleArm != null && new SerializedObject(poleArm).FindProperty("hand").objectReferenceValue == null)
                    throw new InvalidOperationException("Demon weapon hand reference is missing");
                var states = root.GetComponentsInChildren<StrikerState>(true);
                int references = 0;
                foreach (var state in states) {
                    var serialized = new SerializedObject(state);
                    var property = serialized.GetIterator();
                    while (property.Next(true)) {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || !(property.objectReferenceValue is AnimationClip clip)) continue;
                        if (!clip.humanMotion) throw new InvalidOperationException("Non-humanoid clip: " + clip.name);
                        var graph = PlayableGraph.Create("Demon clip validation");
                        try {
                            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                            var playable = AnimationClipPlayable.Create(graph, clip);
                            output.SetSourcePlayable(playable);
                            graph.Play();
                            foreach (var time in new[] { 0f, clip.length * .5f, Mathf.Max(0, clip.length - .01f) }) {
                                playable.SetTime(time);
                                graph.Evaluate(0);
                                var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                                if (float.IsNaN(hips.x) || float.IsInfinity(hips.y) || Vector3.Distance(hips, root.transform.position) > 6f)
                                    throw new InvalidOperationException($"Demon rig left its gameplay origin: {clip.name} at {time}, hips={hips}");
                            }
                        }
                        finally { graph.Destroy(); }
                        references++;
                    }
                }
                Debug.Log($"Demon validation passed: {states.Length} states, {references} humanoid clip references, valid avatar, no missing or Warrior scripts.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
