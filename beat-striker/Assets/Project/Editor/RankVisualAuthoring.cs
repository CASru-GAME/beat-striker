using System;
using System.Linq;
using Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Alice.Editor {
    public static class RankVisualAuthoring {
        const string Project = "Assets/Project/";
        const string BadgePath = Project + "Components/GradeBadges/GradeBadgeView.prefab";
        static readonly Color ink = new Color32(9, 14, 25, 250);
        static readonly Color cyan = new Color32(56, 215, 244, 255);
        static readonly Color pink = new Color32(255, 83, 142, 255);
        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Project + "Fonts/kaiso_up/Kaisotai-Next-UP-B SDF.asset");

        [MenuItem("Beat Striker/Authoring/Rank badges and menu visuals")]
        public static void Author() {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try {
                AuthorBadgePrefab();
                Edit("Transition/Battle/BattleTransition.prefab", AuthorBattle);
                Edit("App/UI/ChoiceDialog.prefab", AuthorDialog);
                Edit("Screen/OnlineRoom/OnlineRoomPlayerView.prefab", AuthorOnlineRow);
                Edit("Screen/OnlineRoom/OnlineRoomCanvas.prefab", AuthorOnlineRoom);
                var scene = EditorSceneManager.OpenScene(Project + "Screen/Ranking/RankingScene.unity");
                AuthorRanking(scene); EditorSceneManager.SaveScene(scene);
                scene = EditorSceneManager.OpenScene(Project + "Screen/Menu/MenuScene.unity");
                AuthorMenu(scene); EditorSceneManager.SaveScene(scene);
                scene = EditorSceneManager.OpenScene(Project + "Screen/Title/TitleScene.unity");
                AuthorTitle(scene); EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[RankVisualAuthoring] Saved six rank badges, battle identities, shared grade views, challenge menu and globe button.");
            } finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        static Object Get(Object target, string field) => new SerializedObject(target).FindProperty(field).objectReferenceValue;
        static void Set(Object target, string field, Object value) {
            var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); Dirty(target);
        }
        static void Dirty(Object target) {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        static T InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        static void Edit(string path, Action<GameObject> edit) {
            var root = PrefabUtility.LoadPrefabContents(Project + path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, Project + path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max) {
            var existing = parent ? parent.Find(name) : null;
            var rect = existing ? (RectTransform)existing : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); Place(rect, min, max); return rect;
        }
        static void Place(RectTransform rect, Vector2 min, Vector2 max) {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; Dirty(rect);
        }
        static TMP_Text Label(Transform parent, string name, string text, float size, Vector2 min, Vector2 max, Color color) {
            var rect = Rect(parent, name, min, max);
            var label = rect.GetComponent<TMP_Text>(); if (!label) label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font; label.fontSharedMaterial = Font.material; label.text = text; label.fontSize = size;
            label.fontSizeMax = size; label.fontSizeMin = size * .6f; label.enableAutoSizing = true;
            label.color = color; label.alignment = TextAlignmentOptions.MidlineLeft; label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Truncate; label.raycastTarget = false;
            Dirty(label); return label;
        }
        static void Plate(RectTransform rect, Color color, float slant = 12) {
            var plate = rect.GetComponent<RankingSlantedGraphic>(); if (!plate) plate = rect.gameObject.AddComponent<RankingSlantedGraphic>();
            plate.color = plate.endColor = color; plate.slant = slant; plate.raycastTarget = false;
        }

        static void AuthorBadgePrefab() {
            GameObject root;
            var exists = AssetDatabase.LoadAssetAtPath<GameObject>(BadgePath);
            if (exists) root = PrefabUtility.LoadPrefabContents(BadgePath);
            else root = new GameObject("GradeBadgeView", typeof(RectTransform), typeof(GradeBadgeView));
            try {
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(280, 44);
                var icon = Rect(rect, "Badge", new Vector2(0, 0), new Vector2(0, 1));
                icon.pivot = new Vector2(0, .5f); icon.sizeDelta = new Vector2(44, 0); icon.anchoredPosition = Vector2.zero;
                var image = icon.GetComponent<Image>(); if (!image) image = icon.gameObject.AddComponent<Image>();
                image.preserveAspect = true; image.raycastTarget = false;
                var label = Label(rect, "Rank name", "ピアニッシモ級", 24, Vector2.zero, Vector2.one, Color.white);
                label.rectTransform.offsetMin = new Vector2(50, 0);
                var view = root.GetComponent<GradeBadgeView>(); Set(view, "badge", image); Set(view, "label", label);
                var so = new SerializedObject(view); var array = so.FindProperty("badges"); array.arraySize = 6;
                for (var i = 0; i < 6; i++) {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Project + $"Components/GradeBadges/{(PlayerGrade)i}.svg");
                    if (!sprite) throw new InvalidOperationException($"Missing SVG sprite for {(PlayerGrade)i}");
                    array.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
                }
                so.ApplyModifiedPropertiesWithoutUndo(); view.Set(0);
                PrefabUtility.SaveAsPrefabAsset(root, BadgePath);
            } finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }
        public static GradeBadgeView Badge(Transform parent, string name, int grade, float size, Vector2 min, Vector2 max, float iconSize) {
            var existing = parent.Find(name);
            var root = existing ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BadgePath), parent);
            root.name = name; Place((RectTransform)root.transform, min, max);
            var view = root.GetComponent<GradeBadgeView>(); view.Set(grade);
            var image = (Image)Get(view, "badge");
            image.rectTransform.sizeDelta = new Vector2(iconSize, 0); Dirty(image.rectTransform); Dirty(image); Dirty(image.gameObject);
            var label = (TMP_Text)Get(view, "label"); label.fontSize = label.fontSizeMax = size; label.fontSizeMin = size * .65f;
            label.rectTransform.offsetMin = new Vector2(iconSize + 6, 0); Dirty(label); Dirty(label.rectTransform); Dirty(root);
            return view;
        }

        public static void AuthorBattle(GameObject root) {
            var view = root.GetComponentInChildren<BattleTransitionView>(true);
            var parent = view.GetComponentInChildren<Canvas>(true).transform.Find("Player identities");
            foreach (var name in new[] { "left identity", "right identity" }) {
                var old = parent.Find(name); if (old) Object.DestroyImmediate(old.gameObject);
            }
            for (var side = 0; side < 2; side++) {
                var color = side == 0 ? cyan : pink;
                var minX = side == 0 ? .025f : .525f;
                var card = Rect(parent, side == 0 ? "Left identity plate" : "Right identity plate", new Vector2(minX, .045f), new Vector2(minX + .45f, .285f));
                Plate(card, ink, side == 0 ? 16 : -16);
                Plate(Rect(card, "Top edge", new Vector2(.025f, .965f), new Vector2(.975f, 1)), color, 3);
                var identity = card.GetComponent<BattleIdentityView>(); if (!identity) identity = card.gameObject.AddComponent<BattleIdentityView>();
                var tag = Label(card, "Player", side == 0 ? "1P  PLAYER" : "2P  PLAYER", 14, new Vector2(.06f, .77f), new Vector2(.94f, .94f), color);
                var name = Label(card, "Player name", "プレイヤー", 30, new Vector2(.06f, .37f), new Vector2(.94f, .79f), Color.white);
                var grade = Badge(card, "Rank", side == 0 ? 3 : 5, 18, new Vector2(.045f, .035f), new Vector2(.94f, .39f), 34);
                Set(identity, "nameLabel", name); Set(identity, "playerLabel", tag); Set(identity, "gradeView", grade);
                Set(view, side == 0 ? "leftIdentity" : "rightIdentity", identity);
            }
            var group = parent.GetComponent<CanvasGroup>(); group.alpha = 1; group.blocksRaycasts = false; group.interactable = false;
            Set(view, "identityGroup", group);
        }

        static void AuthorDialog(GameObject root) {
            var view = root.GetComponent<ChoiceDialogView>(); var panel = (RectTransform)Get(view, "panel");
            var grade = Badge(panel, "Rank", 0, 26, new Vector2(.5f, .47f), new Vector2(.5f, .47f), 44);
            var gradeRect = (RectTransform)grade.transform; gradeRect.sizeDelta = new Vector2(0, 40); Dirty(gradeRect);
            var layout = grade.GetComponent<HorizontalLayoutGroup>(); if (!layout) layout = grade.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false; layout.spacing = 6;
            var fitter = grade.GetComponent<ContentSizeFitter>(); if (!fitter) fitter = grade.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var image = (Image)Get(grade, "badge"); var element = image.GetComponent<LayoutElement>();
            if (!element) element = image.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = element.minHeight = element.preferredHeight = 40;
            var rankLabel = (TMP_Text)Get(grade, "label"); rankLabel.enableAutoSizing = false; Dirty(rankLabel);
            var body = Label(panel, "Player description", "プレイヤー", 28, new Vector2(.08f, .56f), new Vector2(.92f, .80f), Color.white);
            body.alignment = TextAlignmentOptions.Center; body.textWrappingMode = TextWrappingModes.Normal;
            body.gameObject.SetActive(false); Set(view, "gradedDescription", body);
            grade.gameObject.SetActive(false); Set(view, "gradeView", grade);
        }

        public static void AuthorRanking(Scene scene) {
            foreach (var section in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RankingGradeSectionView>(true))) {
                var title = section.transform.Find("Grade title"); if (title) Object.DestroyImmediate(title.gameObject);
                var grade = Badge(section.transform, "Rank", section.Grade, 27, Vector2.zero, Vector2.one, 44);
                grade.transform.SetAsFirstSibling();
                var element = grade.GetComponent<LayoutElement>(); if (!element) element = grade.gameObject.AddComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = 48; Set(section, "gradeView", grade);
            }
            foreach (var label in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)))
                if (label.name == "Replay Invitation") { label.text = "チャレンジで、頂点へ！"; Dirty(label); }
        }

        static void AuthorOnlineRow(GameObject root) {
            var view = root.GetComponent<OnlineRoomPlayerView>();
            var status = (TMP_Text)Get(view, "statusLabel");
            var grade = Badge(status.transform.parent, "Rank", 0, 24, new Vector2(.12f, 0), new Vector2(.74f, .34f), 29);
            Place(status.rectTransform, new Vector2(.75f, 0), new Vector2(.95f, .33f));
            var selection = (GameObject)Get(view, "selectionMark");
            Place((RectTransform)selection.transform, new Vector2(.87f, .48f), new Vector2(.97f, .86f));
            var name = (TMP_Text)Get(view, "nameLabel");
            Place(name.rectTransform, new Vector2(.15f, .35f), new Vector2(.85f, 1));
            Set(view, "gradeView", grade);
        }
        static void AuthorOnlineRoom(GameObject root) {
            var view = root.GetComponentInChildren<OnlineRoomView>(true);
            var name = (TMP_Text)Get(view, "opponentLabel");
            var rect = name.rectTransform;
            Place(rect, new Vector2(0, .76f), new Vector2(1, .86f));
            rect.offsetMin = new Vector2(18, 0); rect.offsetMax = new Vector2(-18, 0); Dirty(rect);
            var grade = Badge(rect.parent, "Opponent rank", 0, 24, new Vector2(.16f, .65f), new Vector2(.95f, .75f), 34);
            grade.gameObject.SetActive(false); Set(view, "opponentGrade", grade);
        }

        public static void AuthorMenu(Scene scene) {
            var view = InScene<MenuPresenterView>(scene); var button = (Component)Get(view, "trialEmitter");
            var sample = ((Component)Get(view, "gotoTutorialEmitter")).GetComponentInChildren<TMP_Text>(true);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.font = sample.font; label.fontSharedMaterial = sample.fontSharedMaterial;
            label.fontStyle = sample.fontStyle; label.fontWeight = sample.fontWeight;
            label.fontSize = sample.fontSize; label.fontSizeMax = sample.fontSizeMax; label.fontSizeMin = sample.fontSizeMin;
            label.enableAutoSizing = true; label.text = "チャレンジ"; label.color = sample.color; Dirty(label);
            var icon = button.GetComponentsInChildren<RawImage>(true).Single(i => i.name == "Chara");
            var source = AssetDatabase.LoadAllAssetsAtPath(Project + "Screen/Menu/Challenge.svg").OfType<Texture2D>().Single();
            icon.texture = source; icon.uvRect = new Rect(0, 0, 1, 1); icon.color = Color.white; Dirty(icon);
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f, .55f); iconRect.pivot = new Vector2(.5f, .5f);
            iconRect.sizeDelta = new Vector2(210, 210); iconRect.anchoredPosition = Vector2.zero; iconRect.localScale = Vector3.one; Dirty(iconRect);
        }

        public static void AuthorTitle(Scene scene) {
            var view = InScene<TitleScene>(scene); var emitter = (Component)Get(view, "onlineEmitter");
            var root = emitter.gameObject; var label = root.transform.Find("Label"); if (label) Object.DestroyImmediate(label.gameObject);
            var plate = root.GetComponent<RankingSlantedGraphic>(); if (plate) Object.DestroyImmediate(plate);
            var image = root.GetComponent<RawImage>(); if (!image) image = root.AddComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Project + "Screen/Title/language_56dp_FFFF_FILL0_wght400_GRAD0_opsz48.png");
            image.color = Color.white; image.raycastTarget = true;
            var rect = (RectTransform)root.transform; rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(54, 54); rect.anchoredPosition = new Vector2(-47, 78); rect.localScale = Vector3.one;
            if (!root.GetComponent<ExpandButton>()) root.AddComponent<ExpandButton>();
            Dirty(root); Dirty(rect); Dirty(image);
        }

        [MenuItem("Beat Striker/Preview/Inspect visual references")]
        static void Inspect() {
            var scene = SceneManager.GetActiveScene();
            foreach (var label in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true))) {
                if (label.name is "Tex" or "Description" or "Opponent Name" || label.transform.parent.name == "Panel")
                    Debug.Log($"[VisualReference] {label.name}: {label.text}; font={AssetDatabase.GetAssetPath(label.font)}; rect={label.rectTransform.anchorMin}/{label.rectTransform.anchorMax}; size={label.rectTransform.sizeDelta}");
            }
        }

        [MenuItem("Beat Striker/Preview/Visuals/Title")]
        static void PreviewTitle() => OpenPreview("Title/TitleScene");
        [MenuItem("Beat Striker/Preview/Visuals/Menu")]
        static void PreviewMenu() => OpenPreview("Menu/MenuScene");
        [MenuItem("Beat Striker/Preview/Visuals/Battle introduction")]
        static void PreviewBattle() {
            OpenPreview("Title/TitleScene");
            PlayerProgressionAuthoring.PreviewBattleIntroduction();
            var view = Resources.FindObjectsOfTypeAll<BattleTransitionView>().Single(v => v.gameObject.scene.IsValid() && v.transform.root.name == "Battle introduction preview");
            var registry = InScene<AppStrikerRegistry>(SceneManager.GetActiveScene());
            var entries = new SerializedObject(registry).FindProperty("strikerEntries");
            var left = (Sprite)entries.GetArrayElementAtIndex(0).FindPropertyRelative("Thumbnail").objectReferenceValue;
            var right = Enumerable.Range(0, entries.arraySize).Select(entries.GetArrayElementAtIndex)
                .Single(e => e.FindPropertyRelative("BattleStriker").intValue == (int)Striker.DemonKing);
            view.SetPortraits(left, (Sprite)right.FindPropertyRelative("Thumbnail").objectReferenceValue);
            SceneView.RepaintAll();
        }
        [MenuItem("Beat Striker/Preview/Visuals/Ranking")]
        static void PreviewRanking() => OpenPreview("Ranking/RankingScene");
        [MenuItem("Beat Striker/Preview/Visuals/Online room")]
        static void PreviewOnlineRoom() {
            OpenPreview("OnlineRoom/OnlineRoomScene");
            var view = InScene<OnlineRoomView>(SceneManager.GetActiveScene());
            var players = Enumerable.Range(0, 6).Select(i => new OnlineRoomPlayer {
                sessionId = $"preview-{i}", scene = "OnlineRoom", canInvite = true,
                player = new PlayerIdentity { name = i == 5 ? "ストライカー" : $"プレイヤー {i + 1}", grade = i }
            }).Reverse().ToArray();
            view.Render(new OnlineRoomRoster(true, players), OnlineDuelUiState.Idle("preview-local"), "preview-5", false, "");
            // The input-focus service raises this nested canvas in Play Mode.
            view.DuelRoot.GetComponent<Canvas>().sortingOrder = view.GetComponent<Canvas>().sortingOrder + 1;
        }
        static void OpenPreview(string scene) {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before previewing.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before previewing.");
            PlayerProgressionAuthoring.HideBattleIntroduction();
            EditorSceneManager.OpenScene(Project + "Screen/" + scene + ".unity");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
    }
}
