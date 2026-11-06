using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Alice.Editor {
    public static class TrialPresentationAuthoring {
        const string Project = "Assets/Project/";
        const string ResultPath = Project + "Battle/UI/View/TrialResultDialog.prefab";
        const string IntroPath = Project + "Screen/CharacterSelect/TrialIntroductionDialog.prefab";
        const string TransitionPath = Project + "Transition/Battle/BattleTransition.prefab";
        static readonly Color cyan = new Color32(160, 216, 226, 255);
        static readonly Color ink = new Color32(9, 16, 29, 255);
        static readonly Color gold = new Color32(255, 222, 105, 255);
        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Project + "Fonts/kaiso_up/Kaisotai-Next-UP-B SDF.asset");

        [MenuItem("Beat Striker/Authoring/Challenge presentations")]
        public static void Author() {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try {
                MakeResult(); MakeIntroduction();
                Edit(Project + "Battle/Battle.prefab", root => {
                    var scope = root.GetComponent<BattleScope>();
                    var old = (ChoiceDialogView)Get(scope, "trialDialog");
                    var parent = old.transform.parent;
                    var next = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ResultPath), parent);
                    var serialized = new SerializedObject(scope);
                    var raycasters = serialized.FindProperty("focusRaycasters");
                    for (var i = 0; i < raycasters.arraySize; i++)
                        if (raycasters.GetArrayElementAtIndex(i).objectReferenceValue == old.InputRaycaster)
                            raycasters.GetArrayElementAtIndex(i).objectReferenceValue = next.GetComponent<FocusGraphicRaycaster>();
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Set(scope, "trialDialog", next.GetComponent<ChoiceDialogView>());
                    Set(scope, "trialResultView", next.GetComponent<TrialResultView>());
                    Object.DestroyImmediate(old.gameObject);
                });
                Edit(TransitionPath, MakeAnnouncement);
                var scene = EditorSceneManager.OpenScene(Project + "Screen/CharacterSelect/SelectScene.unity");
                var select = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SelectScene>(true)).Single();
                var oldIntro = (TrialIntroductionView)Get(select, "trialIntroduction");
                if (oldIntro) Object.DestroyImmediate(oldIntro.gameObject);
                var intro = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(IntroPath), select.CpuDialog.transform.parent);
                Set(select, "trialIntroduction", intro.GetComponent<TrialIntroductionView>());
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[TrialPresentation] Saved result, promotion announcement and narrated introduction with serialized references.");
            } finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        static Object Get(Object target, string field) => new SerializedObject(target).FindProperty(field).objectReferenceValue;
        static void Set(Object target, string field, Object value) {
            var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        static void Number(Object target, string field, float value) {
            var so = new SerializedObject(target); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Array(Object target, string field, Object[] values) {
            var so = new SerializedObject(target); var p = so.FindProperty(field); p.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Edit(string path, Action<GameObject> action) {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height) {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); Place(rect, x, y, width, height); return rect;
        }
        static void Place(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, y);
        }
        static TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height, float size, Color color) {
            var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.fontSharedMaterial = Font.material; text.text = value; text.fontSize = size;
            text.color = color; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.richText = false; text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
        static RankingSlantedGraphic Plate(Transform parent, string name, float x, float y, float width, float height, Color color, float slant = 20) {
            var graphic = Rect(parent, name, x, y, width, height).gameObject.AddComponent<RankingSlantedGraphic>();
            graphic.color = graphic.endColor = color; graphic.slant = slant; graphic.raycastTarget = false; return graphic;
        }
        static Image Badge(Transform parent, string name, int grade, float x, float y, float size) {
            var image = Rect(parent, name, x, y, size, size).gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Project + $"Components/GradeBadges/{(PlayerGrade)grade}.svg");
            image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        static CanvasGroup Group(Transform parent, string name, float x, float y, float width, float height) => Rect(parent, name, x, y, width, height).gameObject.AddComponent<CanvasGroup>();
        static TrialSealGraphic Seal(Transform parent, string name, float size, Color color, float inner, float fill) {
            var seal = Rect(parent, name, 0, 0, size, size).gameObject.AddComponent<TrialSealGraphic>();
            seal.color = color; seal.raycastTarget = false;
            Number(seal, "innerRadius", inner); Number(seal, "segmentFill", fill); return seal;
        }

        static GameObject NewDialog(string name, float width, float height, bool frame) {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Project + "App/UI/ChoiceDialog.prefab"));
            root.name = name;
            var view = root.GetComponent<ChoiceDialogView>();
            var panel = (RectTransform)Get(view, "panel");
            panel.sizeDelta = new Vector2(width, height);
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);
            root.GetComponent<CanvasScaler>().matchWidthOrHeight = .5f;
            var panelGraphic = panel.GetComponent<RankingSlantedGraphic>(); panelGraphic.color = panelGraphic.endColor = ink;
            var heading = (TMP_Text)Get(view, "heading");
            Place(heading.rectTransform, 0, height * .5f - 83, width - 100, 72);
            heading.fontSize = 52; heading.fontSizeMax = 52; heading.fontSizeMin = 32; heading.enableAutoSizing = true; heading.color = cyan;
            var description = (TMP_Text)Get(view, "description");
            description.fontSize = 20; description.fontSizeMin = 16; description.fontSizeMax = 20;
            description.enableAutoSizing = true; description.richText = false;
            Place(description.rectTransform, 0, -height * .5f + 132, width - 110, 52);
            var choices = (RectTransform)panel.Find("Choices");
            var layout = choices.GetComponent<VerticalLayoutGroup>(); if (layout) Object.DestroyImmediate(layout);
            var horizontal = choices.GetComponent<HorizontalLayoutGroup>();
            if (!horizontal) horizontal = choices.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 22; horizontal.childControlWidth = horizontal.childControlHeight = true;
            horizontal.childForceExpandHeight = horizontal.childForceExpandWidth = true;
            Place(choices, 0, -height * .5f + 65, width - 150, 56);
            foreach (Transform child in choices) {
                var element = child.GetComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = 56; element.flexibleWidth = 1;
                child.gameObject.SetActive(child.GetSiblingIndex() < 2);
            }
            var accent = panel.Find("Accent").GetComponent<RankingSlantedGraphic>();
            if (frame) {
                accent.color = accent.endColor = cyan;
                var side = Plate(panel, "Left edge", -width * .5f + 14, 0, 3, height - 80, cyan, 0);
                side.color = side.endColor = new Color(cyan.r, cyan.g, cyan.b, .4f);
                Plate(panel, "Bottom edge", 0, -height * .5f + 15, width - 100, 2, cyan, 0);
            } else {
                Object.DestroyImmediate(accent.gameObject);
            }
            return root;
        }

        static void MakeResult() {
            var root = NewDialog("TrialResultDialog", 1000, 680, frame: false);
            try {
                var dialog = root.GetComponent<ChoiceDialogView>(); var panel = (RectTransform)Get(dialog, "panel");
                root.GetComponent<Image>().color = new Color(0f, 0f, 0f, .85f);
                root.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                Set(dialog, "fullScreenFade", root.AddComponent<CanvasGroup>());
                Object.DestroyImmediate(panel.GetComponent<RankingSlantedGraphic>());
                Object.DestroyImmediate(panel.GetComponent<CanvasRenderer>());
                panel.name = "Result layout";
                var view = root.AddComponent<TrialResultView>();
                var content = Rect(panel, "Challenge result", 0, 0, 1000, 660);
                content.SetSiblingIndex(1);
                var center = Rect(content, "Medallion anchor", 0, 28, 300, 300);
                var medallion = Rect(center, "Medallion", 0, 0, 300, 300);
                var outer = Seal(medallion, "Outer segmented ring", 286, cyan, .95f, 1f);
                var outerSettings = new SerializedObject(outer);
                outerSettings.FindProperty("segments").intValue = 96;
                outerSettings.FindProperty("distressed").boolValue = true;
                outerSettings.ApplyModifiedPropertiesWithoutUndo();
                var inner = Seal(medallion, "Inner ring", 264, cyan, .994f, 1);
                inner.gameObject.SetActive(false);
                var burst = Group(medallion, "Promotion rays", 0, 0, 360, 360);
                var rays = Seal(burst.transform, "Radial rays", 590, cyan, .06f, .52f);
                var raySettings = new SerializedObject(rays);
                raySettings.FindProperty("segments").intValue = 12;
                raySettings.ApplyModifiedPropertiesWithoutUndo();
                Number(rays, "outerOpacity", .14f);
                var badge = Badge(medallion, "Player badge", 0, 0, 0, 222);
                var stamp = Group(medallion, "Failure stamp", 0, 0, 190, 190);
                var red = new Color32(209, 91, 109, 255);
                var seal = Seal(stamp.transform, "Cross seal", 218, red, .965f, 1);
                Number(seal, "crossWidthRatio", .103f);
                var flash = Group(medallion, "Impact flash", 0, 0, 270, 270);
                var flare = Plate(flash.transform, "Light", 0, 0, 270, 270, Color.white, 70); flare.hexagon = true;
                var screenFlash = Rect(root.transform, "Promotion screen flash", 0, 0, 0, 0).gameObject.AddComponent<Image>();
                screenFlash.color = Color.clear; screenFlash.raycastTarget = false;
                var screenFlashRect = screenFlash.rectTransform;
                screenFlashRect.anchorMin = Vector2.zero; screenFlashRect.anchorMax = Vector2.one;
                screenFlashRect.offsetMin = screenFlashRect.offsetMax = Vector2.zero;
                var grade = Text(content, "Earned rank", "ピアニッシモ級", 0, -140, 750, 46, 34, Color.white);
                var detail = Text(content, "Result caption", "", 0, -178, 850, 30, 20, cyan);
                var body = (TMP_Text)Get(dialog, "description");
                Place(body.rectTransform, 0, -222, 860, 46); body.fontSizeMin = 15; body.textWrappingMode = TextWrappingModes.Normal;
                var actions = panel.Find("Choices").gameObject.AddComponent<CanvasGroup>();
                Set(view, "dialog", dialog); Set(view, "content", content.gameObject); Set(view, "badge", badge); Set(view, "gradeLabel", grade);
                Set(view, "detail", detail); Set(view, "medallion", medallion);
                Set(view, "burst", burst); Set(view, "burstTransform", burst.transform); Set(view, "flash", flash);
                Set(view, "screenFlash", screenFlash);
                Set(view, "stamp", stamp); Set(view, "stampTransform", stamp.transform); Set(view, "actions", actions);
                Array(view, "badges", Enumerable.Range(0, 6).Select(i => BadgeSprite(i)).ToArray());
                Array(view, "accents", new Object[] { outer, inner, detail });
                Set(view, "rays", rays);
                Set(view, "spinSound", Clip("Screen/CharacterSelect/mixkit-magic-sweep-game-trophy-257.wav"));
                Set(view, "successSound", Clip("Screen/CharacterSelect/mixkit-robot-positive-item-acquired-3205.wav"));
                Set(view, "failureSound", Clip("Strikers/DarkSatan/Effects/Melee/DarkPunchImpact.wav"));
                Set(view, "promotionSuccessSound", Clip("Battle/UI/View/金属タイトル表示1.mp3"));
                Set(view, "promotionFailureSound", Clip("Battle/UI/View/落ち込む.mp3"));
                burst.alpha = stamp.alpha = flash.alpha = 0;
                content.gameObject.SetActive(false); root.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, ResultPath);
            } finally { Object.DestroyImmediate(root); }
        }
        static Sprite BadgeSprite(int grade) => AssetDatabase.LoadAssetAtPath<Sprite>(Project + $"Components/GradeBadges/{(PlayerGrade)grade}.svg");
        static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(Project + path);

        static void MakeIntroduction() {
            var root = NewDialog("TrialIntroductionDialog", 1150, 660, frame: false);
            try {
                var dialog = root.GetComponent<ChoiceDialogView>(); var panel = (RectTransform)Get(dialog, "panel");
                var view = root.AddComponent<TrialIntroductionView>(); Set(view, "dialog", dialog);
                var yellow = new Color32(255, 220, 0, 255);
                var black = new Color32(8, 8, 10, 255);
                var panelGraphic = panel.GetComponent<RankingSlantedGraphic>(); panelGraphic.color = panelGraphic.endColor = black;
                ((TMP_Text)Get(dialog, "heading")).color = yellow;
                var body = (TMP_Text)Get(dialog, "description");
                Place(body.rectTransform, 285, 30, 440, 245); body.fontSize = body.fontSizeMax = 27; body.fontSizeMin = 23;
                body.textWrappingMode = TextWrappingModes.Normal; body.alignment = TextAlignmentOptions.MidlineLeft; body.lineSpacing = 22;
                var groups = new Object[6]; var labels = new Object[6];
                for (var grade = 0; grade < 6; grade++) {
                    var y = -144 + grade * 53;
                    var row = Group(panel, $"Tier {(PlayerGrade)grade}", -275, y, 520, 50); groups[grade] = row;
                    var tier = Rect(row.transform, "Pyramid tier", -125, 0, 260, 49).gameObject.AddComponent<TrialPyramidTierGraphic>();
                    tier.color = yellow; tier.raycastTarget = false;
                    Number(tier, "topWidth", (5 - grade) / 6f); Number(tier, "bottomWidth", (6 - grade) / 6f);
                    var tierEdge = -125f + 130f * (11f - 2f * grade) / 12f;
                    var badgeEdge = 12.5f;
                    Plate(row.transform, "Rank connector", (tierEdge + badgeEdge) * .5f, 0, badgeEdge - tierEdge, 1, yellow, 0);
                    Badge(row.transform, "Rank badge", grade, 35, 0, 45);
                    var label = Text(row.transform, "Rank name", PlayerGrades.Label((PlayerGrade)grade), 155, 0, 196, 48, 21, yellow);
                    label.alignment = TextAlignmentOptions.MidlineLeft; labels[grade] = label;
                }
                Text(panel, "Rank path", "WIN  ↑  RANK UP", -290, 181, 470, 30, 20, yellow);
                Text(panel, "Retry rule", "敗北しても、階級は下がらない", -275, -204, 500, 30, 20, yellow);
                Array(view, "tiers", groups); Array(view, "tierLabels", labels);
                var source = root.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; Set(view, "narration", source);
                var ids = new[] { "challenge_intro" };
                foreach (var id in ids) {
                    var importer = (AudioImporter)AssetImporter.GetAtPath(Project + $"Screen/CharacterSelect/Voice/{id}.mp3");
                    importer.forceToMono = true; var settings = importer.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.CompressedInMemory; settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    importer.defaultSampleSettings = settings; importer.SaveAndReimport();
                }
                Array(view, "narrationClips", ids.Select(id => Clip($"Screen/CharacterSelect/Voice/{id}.mp3")).ToArray());
                root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, IntroPath);
            } finally { Object.DestroyImmediate(root); }
        }

        static void MakeAnnouncement(GameObject root) {
            var transition = root.GetComponent<BattleTransitionView>();
            var old = (TrialAnnouncementView)Get(transition, "trialAnnouncement");
            if (old) Object.DestroyImmediate(old.gameObject);
            var layer = Rect(root.transform, "Promotion match announcement", 0, 0, 1280, 720);
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one; layer.sizeDelta = Vector2.zero;
            var view = layer.gameObject.AddComponent<TrialAnnouncementView>();
            var group = layer.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var yellow = new Color32(255, 220, 0, 255);
            var black = new Color32(8, 8, 10, 255);
            Plate(layer, "Dim", 0, 0, 2400, 1400, new Color(0, 0, 0, .72f), 0);
            var band = Rect(layer, "Impact band", 0, 0, 1280, 330);
            Plate(band, "Upper stripe", 0, 155, 1500, 5, yellow, 0);
            Plate(band, "Main band", 0, 0, 1500, 300, black, 60);
            Plate(band, "Lower stripe", 0, -155, 1500, 5, yellow, 0);
            var eyebrow = Text(band, "Eyebrow", "CHALLENGE  /  PROMOTION MATCH", 0, 112, 1000, 26, 22, yellow); eyebrow.characterSpacing = 6;
            var grade = Text(band, "Target grade", "フォルティッシモ級", 0, 48, 1080, 70, 53, yellow);
            Text(band, "Promotion match", "昇格戦", 0, -59, 960, 120, 104, yellow);
            var flash = Group(layer, "Impact flash", 0, 0, 2400, 1400);
            Plate(flash.transform, "Flash plate", 0, 0, 2400, 1400, yellow, 0); flash.alpha = 0;
            Set(view, "content", layer.gameObject); Set(view, "group", group); Set(view, "band", band); Set(view, "grade", grade); Set(view, "flash", flash);
            Set(view, "impactSound", Clip("Strikers/DarkSatan/Effects/Melee/DarkPunchImpact.wav"));
            Set(transition, "trialAnnouncement", view); layer.gameObject.SetActive(false);
        }
    }
}
