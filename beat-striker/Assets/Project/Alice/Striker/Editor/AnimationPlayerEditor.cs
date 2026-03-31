using Alice;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Alice.Editor {
    [CustomEditor(typeof(AnimationPlayer))]
    public class AnimationPlayerEditor : UnityEditor.Editor {
        private static readonly FieldInfo AnimatorField = typeof(AnimationPlayer).GetField("animator", BindingFlags.Instance | BindingFlags.NonPublic);

        private static AnimationPlayer previewOwner;
        private static GameObject previewSampleTarget;
        private static AnimationClip previewClip;
        private static float previewTime;
        private static bool isPreviewPlaying;
        private static double lastUpdateTime;
        private static AnimationPlayerEditor activeEditor;

        private AnimationClip localPreviewClip;
        private float localPreviewTime;

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
            EditorGUILayout.LabelField("Preview Animation", EditorStyles.boldLabel);

            if (Application.isPlaying) {
                EditorGUILayout.HelpBox("Animation preview is available only in Edit Mode.", MessageType.Info);
                return;
            }

            var currentPlayer = (AnimationPlayer)target;
            bool isSessionTarget = ReferenceEquals(previewOwner, currentPlayer);
            bool hasActiveSession = previewOwner != null;

            if (hasActiveSession && !isSessionTarget) {
                EditorGUILayout.HelpBox($"Preview is running on: {previewOwner.name}", MessageType.Info);
            }

            bool canPreview = CanPreviewTarget(currentPlayer, out var reason);
            if (!canPreview) {
                EditorGUILayout.HelpBox(reason, MessageType.Info);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                var clipToEdit = isSessionTarget ? previewClip : localPreviewClip;
                var selectedClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", clipToEdit, typeof(AnimationClip), false);
                if (selectedClip != clipToEdit) {
                    if (isSessionTarget) {
                        previewClip = selectedClip;
                        previewTime = 0f;
                        if (previewClip != null) {
                            SampleCurrentFrame();
                        }
                        else {
                            StopPreview(resetPose: true);
                        }
                    }
                    else {
                        localPreviewClip = selectedClip;
                        localPreviewTime = 0f;
                        if (localPreviewClip != null && !hasActiveSession) {
                            SampleOneShot(currentPlayer, localPreviewClip, localPreviewTime);
                        }
                    }
                }

                var clipForUi = isSessionTarget ? previewClip : localPreviewClip;
                var timeForUi = isSessionTarget ? previewTime : localPreviewTime;

                using (new EditorGUI.DisabledScope(clipForUi == null || !canPreview)) {
                    float maxTime = Mathf.Max(clipForUi != null ? clipForUi.length : 0f, 0.01f);
                    var newTime = EditorGUILayout.Slider("Time", timeForUi, 0f, maxTime);
                    if (!Mathf.Approximately(newTime, timeForUi)) {
                        if (isSessionTarget) {
                            previewTime = newTime;
                            SampleCurrentFrame();
                        }
                        else {
                            localPreviewTime = newTime;
                            if (!hasActiveSession) {
                                SampleOneShot(currentPlayer, localPreviewClip, localPreviewTime);
                            }
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope()) {
                        if (!isSessionTarget || !isPreviewPlaying) {
                            if (GUILayout.Button("Play")) {
                                var clipToPlay = isSessionTarget ? previewClip : localPreviewClip;
                                var startTime = isSessionTarget ? previewTime : localPreviewTime;
                                StartPreview(currentPlayer, clipToPlay, startTime);
                            }
                        }
                        else {
                            if (GUILayout.Button("Pause")) {
                                PausePreview();
                            }
                        }

                        if (GUILayout.Button("Stop")) {
                            StopPreview(resetPose: false, clearSession: false);
                        }
                    }
                }
            }
        }

        private void StartPreview(AnimationPlayer owner, AnimationClip clip, float startTime) {
            if (owner == null || clip == null || !CanPreviewTarget(owner, out _)) {
                return;
            }

            previewOwner = owner;
            previewSampleTarget = GetSampleTarget(owner);
            previewClip = clip;
            previewTime = Mathf.Max(0f, startTime);
            isPreviewPlaying = true;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= UpdatePreviewStatic;
            EditorApplication.update += UpdatePreviewStatic;

            SampleCurrentFrame();
        }

        private void PausePreview() {
            isPreviewPlaying = false;
            EditorApplication.update -= UpdatePreviewStatic;
        }

        private void StopPreview(bool resetPose, bool clearSession = true) {
            isPreviewPlaying = false;
            EditorApplication.update -= UpdatePreviewStatic;

            if (clearSession) {
                previewTime = 0f;
                previewOwner = null;
                previewSampleTarget = null;
                previewClip = null;
            }

            if (resetPose && AnimationMode.InAnimationMode()) {
                EditorApplication.delayCall -= StopAnimationModeSafely;
                EditorApplication.delayCall += StopAnimationModeSafely;
            }

            if (activeEditor != null) {
                activeEditor.Repaint();
            }
            SceneView.RepaintAll();
        }

        private void StopAnimationModeSafely() {
            EditorApplication.delayCall -= StopAnimationModeSafely;
            if (AnimationMode.InAnimationMode()) {
                AnimationMode.StopAnimationMode();
            }
            SceneView.RepaintAll();
        }

        private static void UpdatePreviewStatic() {
            if (!isPreviewPlaying) {
                return;
            }

            if (previewClip == null || previewOwner == null || Application.isPlaying || !CanPreviewTarget(previewOwner, out _)) {
                if (activeEditor != null) {
                    activeEditor.StopPreview(resetPose: true, clearSession: true);
                }
                return;
            }

            float clipLength = Mathf.Max(previewClip.length, 0.01f);
            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - lastUpdateTime);
            lastUpdateTime = now;

            previewTime += delta;
            if (previewTime > clipLength) {
                previewTime = Mathf.Repeat(previewTime, clipLength);
            }

            if (activeEditor != null) {
                activeEditor.SampleCurrentFrame();
                activeEditor.Repaint();
            }
            else {
                SampleCurrentFrameStatic();
            }
        }

        private void SampleCurrentFrame() {
            SampleCurrentFrameStatic();
        }

        private static void SampleCurrentFrameStatic() {
            if (previewClip == null || previewOwner == null || !CanPreviewTarget(previewOwner, out _)) {
                return;
            }

            if (previewSampleTarget == null) {
                previewSampleTarget = GetSampleTarget(previewOwner);
            }

            var sampleTarget = previewSampleTarget;
            if (sampleTarget == null) {
                return;
            }

            if (!AnimationMode.InAnimationMode()) {
                AnimationMode.StartAnimationMode();
            }

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(sampleTarget, previewClip, previewTime);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        private static bool CanPreviewTarget(AnimationPlayer animationPlayer, out string reason) {
            reason = string.Empty;
            if (animationPlayer == null) {
                reason = "Target is missing.";
                return false;
            }

            if (EditorUtility.IsPersistent(animationPlayer.gameObject)) {
                reason = "Project view prefab assets cannot be previewed directly. Open Prefab Mode or place it in a scene.";
                return false;
            }

            if (!animationPlayer.gameObject.scene.IsValid() || !animationPlayer.gameObject.scene.isLoaded) {
                reason = "Target is not in a loaded scene.";
                return false;
            }

            if (GetSampleTarget(animationPlayer) == null) {
                reason = "Animator was not found. Assign animator on AnimationPlayer or place Animator under this object.";
                return false;
            }

            return true;
        }

        private static GameObject GetSampleTarget(AnimationPlayer animationPlayer) {
            var animator = AnimatorField != null ? AnimatorField.GetValue(animationPlayer) as Animator : null;
            if (animator == null) {
                animator = animationPlayer.GetComponentInChildren<Animator>();
            }
            return animator != null ? animator.gameObject : null;
        }

        private static void SampleOneShot(AnimationPlayer owner, AnimationClip clip, float time) {
            if (owner == null || clip == null || !CanPreviewTarget(owner, out _)) {
                return;
            }

            var sampleTarget = GetSampleTarget(owner);
            if (sampleTarget == null) {
                return;
            }

            if (!AnimationMode.InAnimationMode()) {
                AnimationMode.StartAnimationMode();
            }

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(sampleTarget, clip, time);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }
    }
}
