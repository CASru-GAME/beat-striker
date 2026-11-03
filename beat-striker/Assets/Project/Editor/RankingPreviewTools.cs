using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Reflection;
using TMPro;

namespace Alice.Editor {
    public static class RankingPreviewTools {
        [MenuItem("Beat Striker/Preview/Refresh ranking layout")]
        public static void RefreshLayout() {
            if (Application.isPlaying) { Debug.LogError("Stop Play Mode first."); return; }
            var view = Object.FindFirstObjectByType<RankingPresenterView>();
            if (!view || view.gameObject.scene.path != "Assets/Project/Screen/Ranking/RankingScene.unity") {
                Debug.LogError("Open RankingScene first."); return;
            }
            const string path = "Assets/Project/Screen/Ranking/RankingHistoryCellView.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try {
                ConfigureTextOverflow(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
            foreach (var sceneRoot in view.gameObject.scene.GetRootGameObjects()) ConfigureTextOverflow(sceneRoot);
            typeof(RankingPresenterView).GetMethod("ConfigureBackButton", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            var back = (Component)new SerializedObject(view).FindProperty("backToMenuEmitter").objectReferenceValue;
            PrefabUtility.RecordPrefabInstancePropertyModifications(back.transform);
            var preview = Object.FindFirstObjectByType<RankingEditorPreview>();
            if (preview) { preview.gameObject.SetActive(false); preview.gameObject.SetActive(true); }
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
        }

        static void ConfigureTextOverflow(GameObject root) {
            foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true)) {
                if (label.overflowMode != TextOverflowModes.Ellipsis) continue;
                label.overflowMode = TextOverflowModes.Truncate;
                EditorUtility.SetDirty(label);
                if (PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
        }

        [MenuItem("Beat Striker/Preview/Place ranking sample cards in scene")]
        public static void PlaceSceneSamples() {
            if (Application.isPlaying) { Debug.LogError("Stop Play Mode first."); return; }
            var view = Object.FindFirstObjectByType<RankingPresenterView>();
            if (!view || view.gameObject.scene.path != "Assets/Project/Screen/Ranking/RankingScene.unity") {
                Debug.LogError("Open RankingScene first."); return;
            }
            var scroll = new SerializedObject(view).FindProperty("battleHistoryScrollRect").objectReferenceValue;
            var viewport = (Component)new SerializedObject(scroll).FindProperty("m_Viewport").objectReferenceValue;
            if (viewport.transform.Find("Editor Preview Matches")) { Debug.LogError("Preview already exists."); return; }
            var preview = new GameObject("Editor Preview Matches", typeof(RectTransform), typeof(RankingEditorPreview));
            preview.tag = "EditorOnly";
            preview.transform.SetParent(viewport.transform, false);
            var previewRect = (RectTransform)preview.transform;
            previewRect.anchorMin = Vector2.zero; previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = previewRect.offsetMax = Vector2.zero;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Screen/Ranking/RankingHistoryCellView.prefab");
            var namesA = new[] { "ビート", "リズム", "ストライカー", "ビート" };
            var namesB = new[] { "ストライカー", "ビート", "リズム", "リズム" };
            var results = new[] { "1P WIN!", "2P WIN!", "DRAW", "1P WIN!" };
            var dates = new[] { "2026/11/03 20:32", "2026/11/03 20:24", "2026/11/03 20:18", "2026/11/03 20:10" };
            for (var i = 0; i < namesA.Length; i++) {
                var row = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview.transform);
                row.name = $"Sample Match {i + 1}";
                var rect = (RectTransform)row.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = new Vector2(604, 132);
                rect.anchoredPosition = new Vector2(0, -132 * i);
                var cell = new SerializedObject(row.GetComponent<RankingHistoryCellView>());
                SetSampleText(cell, "playerALabel", namesA[i]);
                SetSampleText(cell, "playerBLabel", namesB[i]);
                SetSampleText(cell, "resultLabel", results[i]);
                SetSampleText(cell, "dateLabel", dates[i]);
                SetSampleText(cell, "detailLabel", i % 2 == 0 ? "Street / Beat the Night" : "Live / Electric Stage");
                var replay = (Component)cell.FindProperty("replayPlate").objectReferenceValue;
                replay.gameObject.SetActive(i != 2);
            }
            // Apply the same width and aspect ratio as the runtime recycling system.
            preview.SetActive(false);
            preview.SetActive(true);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
        }

        static void SetSampleText(SerializedObject cell, string property, string text) {
            var label = new SerializedObject(cell.FindProperty(property).objectReferenceValue);
            label.FindProperty("m_text").stringValue = text;
            label.ApplyModifiedPropertiesWithoutUndo();
        }
        [MenuItem("Beat Striker/Preview/Build ranking layout")]
        public static void BuildLayout() {
            if (Application.isPlaying) { Debug.LogError("Stop Play Mode first."); return; }
            var view = Object.FindFirstObjectByType<RankingPresenterView>();
            if (!view || view.gameObject.scene.path != "Assets/Project/Screen/Ranking/RankingScene.unity") {
                Debug.LogError("Open RankingScene first."); return;
            }
            if (GameObject.Find("Ranking Backdrop")) { Debug.LogError("Ranking layout already exists."); return; }
            const string path = "Assets/Project/Screen/Ranking/RankingHistoryCellView.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try {
                var cell = root.GetComponent<RankingHistoryCellView>();
                typeof(RankingHistoryCellView).GetMethod("BuildCell", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cell, null);
                EditorUtility.SetDirty(cell);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
            typeof(RankingPresenterView).GetMethod("BuildScreen", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
        }
        // Editor-only sample records; never used by the API client or a player build.
        [MenuItem("Beat Striker/Preview/Ranking sample matches")]
        public static void SampleMatches() {
            var list = Object.FindFirstObjectByType<RankingHistoryListView>();
            if (!Application.isPlaying || !list) {
                Debug.LogWarning("Open RankingScene in Play Mode before previewing match records.");
                return;
            }
            list.SetEntries(new[] {
                new RankingBattleHistoryEntry("preview-1", "ビート", "ストライカー", "2026/11/03 20:32", "1P WIN!", "Street / Beat the Night", true),
                new RankingBattleHistoryEntry("preview-2", "リズム", "ビート", "2026/11/03 20:24", "2P WIN!", "Live / Electric Stage", true),
                new RankingBattleHistoryEntry("preview-3", "ストライカー", "リズム", "2026/11/03 20:18", "DRAW", "Street / Beat the Night", false),
                new RankingBattleHistoryEntry("preview-4", "ビート", "リズム", "2026/11/03 20:10", "1P WIN!", "Live / Electric Stage", true),
                new RankingBattleHistoryEntry("preview-5", "ストライカー", "ビート", "2026/11/03 20:02", "2P WIN!", "Street / Beat the Night", true),
                new RankingBattleHistoryEntry("preview-6", "リズム", "ストライカー", "2026/11/03 19:54", "1P WIN!", "Live / Electric Stage", false)
            });
        }

        [MenuItem("Beat Striker/Preview/Ranking empty")]
        public static void Empty() {
            var list = Object.FindFirstObjectByType<RankingHistoryListView>();
            if (Application.isPlaying && list) list.SetEntries(null);
        }
    }
}
