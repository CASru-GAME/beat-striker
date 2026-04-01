using Alice;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Alice.Editor {
    [InitializeOnLoad]
    [CustomEditor(typeof(EffectPlayer))]
    public class EffectPlayerEditor : UnityEditor.Editor {
        private static readonly FieldInfo EffectPrefabField = typeof(EffectPlayer).GetField("effectPrefab", BindingFlags.Instance | BindingFlags.NonPublic);

        private static EffectPlayer previewOwner;
        private static ParticleSystem previewInstance;
        private static Transform previewAnchor;
        private static double lastUpdateTime;
        private static float previewIntervalSeconds;
        private static float intervalElapsed;
        private static EffectPlayerEditor activeEditor;

        private Transform localPreviewAnchor;
        private float localPreviewIntervalSeconds = 1f;

        static EffectPlayerEditor() {
            AssemblyReloadEvents.beforeAssemblyReload -= ForceCleanup;
            AssemblyReloadEvents.beforeAssemblyReload += ForceCleanup;

            EditorApplication.quitting -= ForceCleanup;
            EditorApplication.quitting += ForceCleanup;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnEnable() {
            activeEditor = this;
        }

        private void OnDisable() {
            if (activeEditor == this) {
                activeEditor = null;
            }
        }

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview Effect", EditorStyles.boldLabel);

            var player = (EffectPlayer)target;
            bool isSessionTarget = ReferenceEquals(previewOwner, player);
            bool canPreview = CanPreview(player, out var reason);

            if (Application.isPlaying) {
                EditorGUILayout.HelpBox("Effect preview is available only in Edit Mode.", MessageType.Info);
                return;
            }

            if (!canPreview) {
                EditorGUILayout.HelpBox(reason, MessageType.Info);
            }

            if (previewOwner != null && !isSessionTarget) {
                EditorGUILayout.HelpBox($"Preview is running on: {previewOwner.name}", MessageType.Info);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                var anchorToEdit = isSessionTarget ? previewAnchor : localPreviewAnchor;
                var selectedAnchor = (Transform)EditorGUILayout.ObjectField("Anchor", anchorToEdit, typeof(Transform), true);
                var intervalToEdit = isSessionTarget ? previewIntervalSeconds : localPreviewIntervalSeconds;
                var selectedInterval = EditorGUILayout.FloatField("Interval Seconds", intervalToEdit);
                if (selectedInterval < 0f) {
                    selectedInterval = 0f;
                }

                if (selectedAnchor != anchorToEdit) {
                    if (isSessionTarget) {
                        previewAnchor = selectedAnchor;
                        ApplyPreviewTransform();
                    }
                    else {
                        localPreviewAnchor = selectedAnchor;
                    }
                }

                if (!Mathf.Approximately(selectedInterval, intervalToEdit)) {
                    if (isSessionTarget) {
                        previewIntervalSeconds = selectedInterval;
                        intervalElapsed = 0f;
                    }
                    else {
                        localPreviewIntervalSeconds = selectedInterval;
                    }
                }

                using (new EditorGUILayout.HorizontalScope()) {
                    using (new EditorGUI.DisabledScope(!canPreview)) {
                        if (GUILayout.Button("Play")) {
                            var anchor = isSessionTarget ? previewAnchor : localPreviewAnchor;
                            var interval = isSessionTarget ? previewIntervalSeconds : localPreviewIntervalSeconds;
                            StartPreview(player, anchor, interval);
                        }
                    }

                    if (GUILayout.Button("Stop")) {
                        StopPreview();
                    }
                }
            }
        }

        private static void StartPreview(EffectPlayer owner, Transform anchor, float intervalSeconds) {
            if (!CanPreview(owner, out _)) {
                return;
            }

            var prefab = GetEffectPrefab(owner);
            if (prefab == null) {
                return;
            }

            StopPreview();

            previewOwner = owner;
            previewAnchor = anchor;
            previewIntervalSeconds = Mathf.Max(0f, intervalSeconds);
            intervalElapsed = 0f;
            previewInstance = Object.Instantiate(prefab, owner.transform);
            previewInstance.gameObject.hideFlags = HideFlags.DontSaveInEditor;
            ApplyPreviewTransform();

            previewInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            previewInstance.Play(true);
            lastUpdateTime = EditorApplication.timeSinceStartup;

            EditorApplication.update -= UpdatePreview;
            EditorApplication.update += UpdatePreview;

            SceneView.RepaintAll();
            if (activeEditor != null) {
                activeEditor.Repaint();
            }
        }

        private static void StopPreview() {
            EditorApplication.update -= UpdatePreview;

            if (previewInstance != null) {
                previewInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                previewInstance.Clear(true);
                Object.DestroyImmediate(previewInstance.gameObject);
            }

            previewInstance = null;
            previewOwner = null;
            previewAnchor = null;
            SceneView.RepaintAll();

            if (activeEditor != null) {
                activeEditor.Repaint();
            }
        }

        private static void UpdatePreview() {
            if (Application.isPlaying || previewOwner == null || previewInstance == null) {
                StopPreview();
                return;
            }

            if (!CanPreview(previewOwner, out _)) {
                StopPreview();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Max((float)(now - lastUpdateTime), 0f);
            lastUpdateTime = now;

            ApplyPreviewTransform();
            previewInstance.Simulate(deltaTime, true, false, false);

            if (previewIntervalSeconds > 0f) {
                intervalElapsed += deltaTime;
                if (intervalElapsed >= previewIntervalSeconds) {
                    intervalElapsed = 0f;
                    previewInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ApplyPreviewTransform();
                    previewInstance.Play(true);
                }
            }

            if (!previewInstance.main.loop && !previewInstance.IsAlive(true)) {
                StopPreview();
                return;
            }

            SceneView.RepaintAll();
            if (activeEditor != null) {
                activeEditor.Repaint();
            }
        }

        private static void ApplyPreviewTransform() {
            if (previewInstance == null || previewOwner == null) {
                return;
            }

            var source = previewAnchor != null ? previewAnchor : previewOwner.transform;
            var t = previewInstance.transform;
            t.SetPositionAndRotation(source.position, source.rotation);
            t.localScale = source.lossyScale;
        }

        private static bool CanPreview(EffectPlayer player, out string reason) {
            reason = string.Empty;
            if (player == null) {
                reason = "Target is missing.";
                return false;
            }

            if (EditorUtility.IsPersistent(player.gameObject)) {
                reason = "Project view prefab assets cannot be previewed directly. Open Prefab Mode or place it in a scene.";
                return false;
            }

            if (!player.gameObject.scene.IsValid() || !player.gameObject.scene.isLoaded) {
                reason = "Target is not in a loaded scene.";
                return false;
            }

            if (GetEffectPrefab(player) == null) {
                reason = "Effect Prefab is not assigned.";
                return false;
            }

            return true;
        }

        private static ParticleSystem GetEffectPrefab(EffectPlayer player) {
            return EffectPrefabField != null ? EffectPrefabField.GetValue(player) as ParticleSystem : null;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode) {
                StopPreview();
            }
        }

        private static void ForceCleanup() {
            StopPreview();
        }
    }
}
