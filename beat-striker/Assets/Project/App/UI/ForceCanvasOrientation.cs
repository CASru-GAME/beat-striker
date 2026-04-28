using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Alice {
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class ForceCanvasOrientation : MonoBehaviour {
        [SerializeField] string rotationRootName = "RotationRoot";
        [SerializeField] bool forceLandscapeWhenPortrait = true;
        [SerializeField] bool includeInactiveCanvases = true;
        [SerializeField] bool includeWorldSpaceCanvases;
        [SerializeField] bool includeNestedCanvases;
        [SerializeField] bool swapCanvasScalerReference = true;
        [SerializeField, Min(1)] int canvasRefreshIntervalFrames = 30;

        readonly Dictionary<Canvas, CanvasSnapshot> snapshotsByCanvas = new();
        readonly List<Canvas> staleCanvases = new();
        readonly List<Transform> directChildren = new();
        readonly List<Transform> orderedChildren = new();

        int framesUntilRefresh;
        bool isPortraitApplied;

        sealed class CanvasSnapshot {
            public RectTransform RotationRoot;
            public readonly List<Transform> OriginalChildOrder = new();
            public readonly bool HasScaler;
            public readonly CanvasScaler.ScaleMode UiScaleMode;
            public readonly CanvasScaler.ScreenMatchMode ScreenMatchMode;
            public readonly Vector2 ReferenceResolution;
            public readonly float MatchWidthOrHeight;
            public readonly float ScaleFactor;

            public CanvasSnapshot(CanvasScaler scaler) {
                HasScaler = scaler;
                if (!HasScaler) {
                    UiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    ScreenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    ReferenceResolution = Vector2.zero;
                    MatchWidthOrHeight = 0f;
                    ScaleFactor = 1f;
                    return;
                }

                UiScaleMode = scaler.uiScaleMode;
                ScreenMatchMode = scaler.screenMatchMode;
                ReferenceResolution = scaler.referenceResolution;
                MatchWidthOrHeight = scaler.matchWidthOrHeight;
                ScaleFactor = scaler.scaleFactor;
            }
        }

        void OnEnable() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshCanvases();
            ApplyOrientation();
        }

        void OnDisable() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreAllCanvases();
            snapshotsByCanvas.Clear();
        }

        void LateUpdate() {
            framesUntilRefresh--;
            if (framesUntilRefresh <= 0) {
                RefreshCanvases();
            }

            ApplyOrientation();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            RefreshCanvases();
            ApplyOrientation();
        }

        void RefreshCanvases() {
            framesUntilRefresh = canvasRefreshIntervalFrames;
            var canvases = Object.FindObjectsByType<Canvas>(
                includeInactiveCanvases ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            staleCanvases.Clear();
            foreach (var canvas in snapshotsByCanvas.Keys) {
                staleCanvases.Add(canvas);
            }

            for (var i = 0; i < canvases.Length; i++) {
                var canvas = canvases[i];
                if (!ShouldManageCanvas(canvas)) {
                    continue;
                }

                if (!snapshotsByCanvas.ContainsKey(canvas)) {
                    canvas.TryGetComponent<CanvasScaler>(out var scaler);
                    var snapshot = new CanvasSnapshot(scaler);
                    CaptureOriginalChildOrder((RectTransform)canvas.transform, snapshot);
                    snapshotsByCanvas[canvas] = snapshot;
                }

                staleCanvases.Remove(canvas);
            }

            for (var i = 0; i < staleCanvases.Count; i++) {
                snapshotsByCanvas.Remove(staleCanvases[i]);
            }
        }

        void ApplyOrientation() {
            var screenWidth = Mathf.Max(1f, Screen.width);
            var screenHeight = Mathf.Max(1f, Screen.height);
            var shouldApplyPortrait = forceLandscapeWhenPortrait && screenWidth < screenHeight;

            if (!shouldApplyPortrait) {
                if (isPortraitApplied) {
                    RestoreAllCanvases();
                }

                isPortraitApplied = false;
                return;
            }

            isPortraitApplied = true;
            foreach (var pair in snapshotsByCanvas) {
                var canvas = pair.Key;
                if (!canvas || canvas.transform is not RectTransform canvasRect) {
                    continue;
                }

                ApplyPortraitRotation(canvas, canvasRect, pair.Value);
            }
        }

        void ApplyPortraitRotation(Canvas canvas, RectTransform canvasRect, CanvasSnapshot snapshot) {
            if (swapCanvasScalerReference && snapshot.HasScaler && canvas.TryGetComponent<CanvasScaler>(out var scaler)) {
                ApplyPortraitCanvasScaler(scaler, snapshot);
            }

            var rotationRoot = GetOrCreateRotationRoot(canvasRect, snapshot);
            var canvasSize = canvasRect.rect.size;
            rotationRoot.anchorMin = new Vector2(0.5f, 0.5f);
            rotationRoot.anchorMax = new Vector2(0.5f, 0.5f);
            rotationRoot.pivot = new Vector2(0.5f, 0.5f);
            rotationRoot.anchoredPosition = Vector2.zero;
            rotationRoot.sizeDelta = new Vector2(canvasSize.y, canvasSize.x);
            rotationRoot.localScale = Vector3.one;
            rotationRoot.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        void ApplyPortraitCanvasScaler(CanvasScaler scaler, CanvasSnapshot snapshot) {
            scaler.uiScaleMode = snapshot.UiScaleMode;
            scaler.scaleFactor = snapshot.ScaleFactor;

            if (snapshot.UiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) {
                return;
            }

            scaler.screenMatchMode = snapshot.ScreenMatchMode;
            scaler.referenceResolution = new Vector2(snapshot.ReferenceResolution.y, snapshot.ReferenceResolution.x);
            scaler.matchWidthOrHeight = 1f - snapshot.MatchWidthOrHeight;
        }

        bool ShouldManageCanvas(Canvas canvas) {
            if (!canvas || !canvas.gameObject.scene.IsValid()) {
                return false;
            }

            if (!includeWorldSpaceCanvases && canvas.renderMode == RenderMode.WorldSpace) {
                return false;
            }

            if (snapshotsByCanvas.ContainsKey(canvas)) {
                return true;
            }

            return includeNestedCanvases || canvas.transform.parent == null || canvas.transform.parent.GetComponentInParent<Canvas>() == null;
        }

        RectTransform GetOrCreateRotationRoot(RectTransform canvasRect, CanvasSnapshot snapshot) {
            if (snapshot.RotationRoot) {
                MoveDirectChildrenUnderRoot(canvasRect, snapshot.RotationRoot, snapshot);
                return snapshot.RotationRoot;
            }

            for (var i = 0; i < canvasRect.childCount; i++) {
                var child = canvasRect.GetChild(i);
                if (child.name == rotationRootName && child is RectTransform existingRoot) {
                    snapshot.RotationRoot = existingRoot;
                    MoveDirectChildrenUnderRoot(canvasRect, existingRoot, snapshot);
                    return existingRoot;
                }
            }

            var rootObject = new GameObject(rotationRootName, typeof(RectTransform));
            var rotationRoot = rootObject.GetComponent<RectTransform>();
            rotationRoot.SetParent(canvasRect, false);
            snapshot.RotationRoot = rotationRoot;
            MoveDirectChildrenUnderRoot(canvasRect, rotationRoot, snapshot);
            rotationRoot.SetAsFirstSibling();
            return rotationRoot;
        }

        void CaptureOriginalChildOrder(RectTransform canvasRect, CanvasSnapshot snapshot) {
            snapshot.OriginalChildOrder.Clear();
            for (var i = 0; i < canvasRect.childCount; i++) {
                var child = canvasRect.GetChild(i);
                if (child.name == rotationRootName && child is RectTransform rotationRoot) {
                    snapshot.RotationRoot = rotationRoot;
                    for (var j = 0; j < rotationRoot.childCount; j++) {
                        snapshot.OriginalChildOrder.Add(rotationRoot.GetChild(j));
                    }
                    continue;
                }

                snapshot.OriginalChildOrder.Add(child);
            }
        }

        void MoveDirectChildrenUnderRoot(RectTransform canvasRect, RectTransform rotationRoot, CanvasSnapshot snapshot) {
            directChildren.Clear();
            for (var i = 0; i < canvasRect.childCount; i++) {
                var child = canvasRect.GetChild(i);
                if (child == rotationRoot) {
                    continue;
                }

                directChildren.Add(child);
            }

            for (var i = 0; i < directChildren.Count; i++) {
                var child = directChildren[i];
                if (child.parent != rotationRoot) {
                    child.SetParent(rotationRoot, false);
                }
            }

            RestoreOriginalChildOrder(rotationRoot, snapshot);
        }

        void RestoreOriginalChildOrder(RectTransform rotationRoot, CanvasSnapshot snapshot) {
            orderedChildren.Clear();

            for (var i = 0; i < snapshot.OriginalChildOrder.Count; i++) {
                var child = snapshot.OriginalChildOrder[i];
                if (child && child.parent == rotationRoot) {
                    orderedChildren.Add(child);
                }
            }

            for (var i = 0; i < rotationRoot.childCount; i++) {
                var child = rotationRoot.GetChild(i);
                if (!orderedChildren.Contains(child)) {
                    orderedChildren.Add(child);
                    snapshot.OriginalChildOrder.Add(child);
                }
            }

            for (var i = 0; i < orderedChildren.Count; i++) {
                orderedChildren[i].SetSiblingIndex(i);
            }
        }

        void RestoreAllCanvases() {
            foreach (var pair in snapshotsByCanvas) {
                var canvas = pair.Key;
                if (!canvas) {
                    continue;
                }

                RestoreCanvas(canvas, pair.Value);
            }
        }

        static void RestoreCanvas(Canvas canvas, CanvasSnapshot snapshot) {
            if (snapshot.RotationRoot) {
                snapshot.RotationRoot.localRotation = Quaternion.identity;
                snapshot.RotationRoot.localScale = Vector3.one;
            }

            if (!snapshot.HasScaler || !canvas.TryGetComponent<CanvasScaler>(out var scaler)) {
                return;
            }

            scaler.uiScaleMode = snapshot.UiScaleMode;
            scaler.screenMatchMode = snapshot.ScreenMatchMode;
            scaler.referenceResolution = snapshot.ReferenceResolution;
            scaler.matchWidthOrHeight = snapshot.MatchWidthOrHeight;
            scaler.scaleFactor = snapshot.ScaleFactor;
        }
    }
}
