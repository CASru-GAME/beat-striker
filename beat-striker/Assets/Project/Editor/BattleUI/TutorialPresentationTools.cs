using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Alice.Editor {
    // Authoring only: every element and reference is saved into editable prefabs.
    public static class TutorialPresentationTools {
        const string BattlePath = "Assets/Project/Battle/Battle.prefab";
        const string GuidePath = "Assets/Project/Battle/UI/Prefabs/TutorialInputGuide.prefab";

        [MenuItem("Tools/Battle/Configure Tutorial Presentation")]
        public static void Configure() {
            ConfigureGuide();
            var battle = PrefabUtility.LoadPrefabContents(BattlePath);
            try {
                var tutorial = battle.GetComponentsInChildren<BattleTutorialView>(true).Single();
                var view = new SerializedObject(tutorial);
                var guide = (TutorialInputGuideView)view.FindProperty("inputGuide").objectReferenceValue;
                Stretch((RectTransform)guide.transform);
                guide.gameObject.SetActive(false);
                var walk = (GameObject)view.FindProperty("walkPracticePanel").objectReferenceValue;
                view.FindProperty("walkPracticeText").objectReferenceValue = walk.GetComponentsInChildren<TMP_Text>(true)
                    .Single(label => label.name == "Text (TMP)");
                var practice = (GameObject)view.FindProperty("characterPracticePanel").objectReferenceValue;
                foreach (var label in practice.GetComponentsInChildren<TMP_Text>(true)) {
                    if (label.name is "LessonTitle" or "LessonProgress") Object.DestroyImmediate(label.gameObject);
                }
                ConfigureBands(view);
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(battle, BattlePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(battle); }
            Debug.Log("Tutorial controls saved at the lower left and right edges.");
        }

        static void ConfigureBands(SerializedObject view) {
            var hints = new List<TMP_Text>();
            foreach (var property in new[] { "overviewPanel", "hpDescriptionPanel", "spDescriptionPanel",
                "walkDescriptionPanel", "walkPracticePanel", "rhythmDescriptionPanel", "rhythmPracticePanel",
                "characterDescriptionPanel", "characterPracticePanel", "timerDescriptionPanel", "victoryDescriptionPanel",
                "scoreDescriptionPanel", "defeatPracticePanel", "scoreAwardDescriptionPanel", "finalPanel" }) {
                var panel = (GameObject)view.FindProperty(property).objectReferenceValue;
                var labels = panel.GetComponentsInChildren<TMP_Text>(true);
                var body = labels.SingleOrDefault(label => label.name == "Text (TMP)");
                if (body == null) continue;
                var description = property.EndsWith("DescriptionPanel") || property is "overviewPanel" or "finalPanel";
                var hint = labels.SingleOrDefault(label => label.name is "Text (TMP) (1)" or "ContinueHint");
                if (description && hint == null) {
                    hint = Object.Instantiate(body, body.transform.parent);
                    hint.name = "ContinueHint";
                }
                if (hint != null) {
                    hint.gameObject.SetActive(description);
                    if (description) {
                        hint.text = "Lキーで次へ";
                        hint.alignment = TextAlignmentOptions.Right;
                        hints.Add(hint);
                    }
                }
                var band = (RectTransform)body.transform.parent;
                var layout = band.GetComponent<TutorialBandLayoutView>();
                if (layout == null) layout = band.gameObject.AddComponent<TutorialBandLayoutView>();
                var serialized = new SerializedObject(layout);
                serialized.FindProperty("band").objectReferenceValue = band;
                serialized.FindProperty("body").objectReferenceValue = body;
                serialized.FindProperty("title").objectReferenceValue = labels.SingleOrDefault(label => label.name == "LessonTitle");
                serialized.FindProperty("continueHint").objectReferenceValue = description ? hint : null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                body.textWrappingMode = TextWrappingModes.Normal;
                var canvas = band.GetComponentsInParent<Canvas>(true).Last();
                layout.Refresh(canvas.GetComponent<CanvasScaler>().referenceResolution.x);
            }
            var array = view.FindProperty("continueHints");
            array.arraySize = hints.Count;
            for (var i = 0; i < hints.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = hints[i];
        }

        static void ConfigureGuide() {
            var root = PrefabUtility.LoadPrefabContents(GuidePath);
            try {
                var view = new SerializedObject(root.GetComponent<TutorialInputGuideView>());
                Stretch((RectTransform)root.transform);
                // The controls themselves carry the animation; no enclosing panel or duplicate instruction.
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) {
                    if (child.name is "Backdrop" or "TopAccent" or "InputLabel" or "ActionCaption" or "DirectionNote")
                        Object.DestroyImmediate(child.gameObject);
                }
                var keyboard = (GameObject)view.FindProperty("keyboardLayout").objectReferenceValue;
                var pad = (GameObject)view.FindProperty("gamepadLayout").objectReferenceValue;
                Stretch((RectTransform)keyboard.transform);
                Stretch((RectTransform)pad.transform);
                var keyboardLeft = Edge(keyboard.transform, "LeftControls", false);
                var keyboardRight = Edge(keyboard.transform, "RightControls", true);
                var padLeft = Edge(pad.transform, "LeftControls", false);
                var padRight = Edge(pad.transform, "RightControls", true);
                var keys = view.FindProperty("keyboardControls");
                for (var i = 0; i < keys.arraySize; i++) {
                    var binding = keys.GetArrayElementAtIndex(i);
                    var control = (TutorialControl)binding.FindPropertyRelative("Control").intValue;
                    var face = (TutorialControlGlyph)binding.FindPropertyRelative("Face").objectReferenceValue;
                    var left = control is TutorialControl.Up or TutorialControl.Left or TutorialControl.Down or TutorialControl.Right or TutorialControl.Select;
                    var position = control switch {
                        TutorialControl.Up => new Vector2(-12f, 12f),
                        TutorialControl.Left => new Vector2(-30f, -12f),
                        TutorialControl.Down => new Vector2(-6f, -12f),
                        TutorialControl.Right => new Vector2(18f, -12f),
                        TutorialControl.Select => new Vector2(12f, 12f),
                        TutorialControl.Start => new Vector2(-23f, 12f),
                        TutorialControl.North => new Vector2(1f, 12f),
                        TutorialControl.West => new Vector2(-17f, -12f),
                        TutorialControl.South => new Vector2(7f, -12f),
                        _ => new Vector2(31f, -12f),
                    };
                    face.transform.SetParent(left ? keyboardLeft : keyboardRight, false);
                    face.rectTransform.anchoredPosition = position;
                    face.rectTransform.localScale = Vector3.one;
                    face.rectTransform.sizeDelta = Vector2.one * 21f;
                }
                var buttons = view.FindProperty("gamepadControls");
                for (var i = 0; i < buttons.arraySize; i++) {
                    var binding = buttons.GetArrayElementAtIndex(i);
                    var control = (TutorialControl)binding.FindPropertyRelative("Control").intValue;
                    var face = (TutorialControlGlyph)binding.FindPropertyRelative("Face").objectReferenceValue;
                    face.transform.SetParent(control == TutorialControl.Select ? padLeft : padRight, false);
                    face.rectTransform.localScale = Vector3.one;
                    face.rectTransform.anchoredPosition = control switch {
                        TutorialControl.North => new Vector2(10f, 22f),
                        TutorialControl.West => new Vector2(-12f, 0f),
                        TutorialControl.South => new Vector2(10f, -22f),
                        TutorialControl.East => new Vector2(32f, 0f),
                        TutorialControl.Select => new Vector2(34f, 8f),
                        _ => new Vector2(-36f, 8f),
                    };
                    if (control is not (TutorialControl.Select or TutorialControl.Start)) face.rectTransform.sizeDelta = Vector2.one * 20f;
                }
                foreach (var label in pad.GetComponentsInChildren<TMP_Text>(true)) {
                    if (label.name is not ("SelectLabel" or "StartLabel")) continue;
                    var left = label.name == "SelectLabel";
                    label.transform.SetParent(left ? padLeft : padRight, false);
                    label.rectTransform.anchoredPosition = new Vector2(left ? 34f : -36f, -6f);
                }
                var gate = (TutorialControlGlyph)view.FindProperty("stickBase").objectReferenceValue;
                gate.transform.parent.SetParent(padLeft, false);
                ((RectTransform)gate.transform.parent).anchoredPosition = new Vector2(-15f, 0f);
                foreach (var glyph in root.GetComponentsInChildren<TutorialControlGlyph>(true)) {
                    var serialized = new SerializedObject(glyph);
                    serialized.FindProperty("activeFill").colorValue = new Color(0.85f, 0.12f, 0.17f, 1f);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                Set(view, "beatSeconds", 0.55f);
                Set(view, "repeatPauseBeats", 3f);
                Set(view, "directionLeadBeats", 0.35f);
                Set(view, "pressBeats", 0.1f);
                Set(view, "holdBeats", 0.2f);
                Set(view, "releaseBeats", 0.15f);
                Set(view, "pressScaleReduction", 0.25f);
                Set(view, "keyTravel", 3f);
                Set(view, "stickTravel", 11f);
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, GuidePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Set(SerializedObject view, string field, float value) => view.FindProperty(field).floatValue = value;

        static void Stretch(RectTransform rect) {
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 76f);
        }

        static RectTransform Edge(Transform parent, string name, bool right) {
            var existing = parent.Cast<Transform>().SingleOrDefault(child => child.name == name);
            var rect = existing == null ? (RectTransform)new GameObject(name, typeof(RectTransform)).transform : (RectTransform)existing;
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(94f, 64f);
            rect.anchoredPosition = new Vector2(right ? -54f : 54f, 37f);
            return rect;
        }
    }
}

