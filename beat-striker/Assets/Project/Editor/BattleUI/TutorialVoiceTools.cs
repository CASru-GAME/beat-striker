using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Alice.Editor {
    public static class TutorialVoiceTools {
        const string ROOT = "Assets/Project/Battle/UI/Tutorial/Voice";
        const string LIBRARY = ROOT + "/TutorialVoice.asset";
        const string BATTLE = "Assets/Project/Battle/Battle.prefab";

        [Serializable] record Manifest {
            public Clip[] Clips;
            public Line[] Lines;
        }
        [Serializable] record Clip {
            public string Id;
            public string Text;
        }
        [Serializable] record Line {
            public string Key;
            public string[] Segments;
        }

        [MenuItem("Tools/Battle/Configure Tutorial Narration")]
        public static void Configure() {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ROOT + "/voice-lines.json"));
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var missingEntries = settings.DefaultGroup.entries.Where(entry =>
                entry.address.StartsWith(ROOT + "/", StringComparison.Ordinal) &&
                string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(entry.guid))).ToArray();
            foreach (var entry in missingEntries) settings.RemoveAssetEntry(entry.guid);
            var library = AssetDatabase.LoadAssetAtPath<TutorialVoiceLibrary>(LIBRARY);
            if (library == null) {
                library = ScriptableObject.CreateInstance<TutorialVoiceLibrary>();
                AssetDatabase.CreateAsset(library, LIBRARY);
            }
            var serializedLibrary = new SerializedObject(library);
            var clips = serializedLibrary.FindProperty("clips");
            clips.arraySize = manifest.Clips.Length;
            for (var i = 0; i < manifest.Clips.Length; i++) {
                var clip = manifest.Clips[i];
                var path = ROOT + "/" + clip.Id + ".mp3";
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null) {
                    throw new InvalidOperationException("Generate the tutorial voice first: " + path);
                }
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = true;
                var sampleSettings = importer.defaultSampleSettings;
                sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                sampleSettings.quality = 0.7f;
                importer.defaultSampleSettings = sampleSettings;
                importer.SaveAndReimport();

                var guid = AssetDatabase.AssetPathToGUID(path);
                var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
                entry.address = path;
                var element = clips.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = clip.Id;
                element.FindPropertyRelative("Audio.m_AssetGUID").stringValue = guid;
            }
            var lines = serializedLibrary.FindProperty("lines");
            lines.arraySize = manifest.Lines.Length;
            for (var i = 0; i < manifest.Lines.Length; i++) {
                var line = manifest.Lines[i];
                var element = lines.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Key").intValue = (int)Enum.Parse<TutorialUiKey>(line.Key);
                var segments = element.FindPropertyRelative("Segments");
                segments.arraySize = line.Segments.Length;
                for (var j = 0; j < line.Segments.Length; j++) {
                    segments.GetArrayElementAtIndex(j).stringValue = line.Segments[j];
                }
            }
            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);

            var root = PrefabUtility.LoadPrefabContents(BATTLE);
            try {
                var view = root.GetComponentsInChildren<BattleTutorialView>(true).Single();
                var narration = view.GetComponent<TutorialNarrationView>();
                if (narration == null) narration = view.gameObject.AddComponent<TutorialNarrationView>();
                var source = view.GetComponent<AudioSource>();
                if (source == null) source = view.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                var serializedNarration = new SerializedObject(narration);
                serializedNarration.FindProperty("library").objectReferenceValue = library;
                serializedNarration.FindProperty("narrationSource").objectReferenceValue = source;
                serializedNarration.ApplyModifiedPropertiesWithoutUndo();

                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("narration").objectReferenceValue = narration;
                var description = (GameObject)serializedView.FindProperty("rhythmDescriptionPanel").objectReferenceValue;
                var descriptionText = description.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "Text (TMP)");
                serializedView.FindProperty("rhythmDescriptionText").objectReferenceValue = descriptionText;
                serializedView.FindProperty("rhythmDescriptionFormat").stringValue =
                    "リズムに合わせて押さないと、コマンドは発動しないのだ！\n{0} のどれかを3拍連続で成功させるのだ";
                serializedView.FindProperty("rhythmPracticeFormat").stringValue =
                    "{2} のどれかをリズムに合わせて押そう\n3回連続成功！ {0} / {1}（ミス・押し忘れでやり直し）";
                var practiceText = (TMP_Text)serializedView.FindProperty("rhythmPracticeText").objectReferenceValue;
                descriptionText.text = "リズムに合わせて押さないと、コマンドは発動しないのだ！\nA / B / X / Y ボタンのどれかを3拍連続で成功させるのだ";
                practiceText.text = "A / B / X / Y ボタンのどれかをリズムに合わせて押そう\n3回連続成功！ 0 / 3（ミス・押し忘れでやり直し）";
                ConfigureEndingPanels(root, serializedView);
                serializedView.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BATTLE);
            }
            finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssetIfDirty(library);
            AssetDatabase.SaveAssetIfDirty(settings.DefaultGroup);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log($"Tutorial narration configured: {manifest.Lines.Length} pages, {manifest.Clips.Length} Addressable clips. VOICEVOX:ずんだもん");
        }
        static void ConfigureEndingPanels(GameObject root, SerializedObject view) {
            var battleView = root.GetComponentsInChildren<BattlePresenterView>(true).Single();
            var serializedBattleView = new SerializedObject(battleView);
            view.FindProperty("battleView").objectReferenceValue = battleView;
            var timerText = (TMP_Text)serializedBattleView.FindProperty("remainingBeatCountText").objectReferenceValue;
            view.FindProperty("timerPointerTarget").objectReferenceValue = timerText.rectTransform;
            ConfigurePanel(view, "timerDescriptionPanel", "hpDescriptionPanel", "TimerDescription",
                "これがタイマーなのだ。曲の残り拍数を表示しているのだ\nこの数字が0になると、試合終了なのだ");
            var timerPanel = (GameObject)view.FindProperty("timerDescriptionPanel").objectReferenceValue;
            ((RectTransform)timerPanel.transform).anchoredPosition = new Vector2(0f, -90f);
            ConfigurePanel(view, "victoryDescriptionPanel", "hpDescriptionPanel", "VictoryDescription",
                "曲が終わったとき、<b>カップが多い方が勝ち</b>なのだ！\n同じ数なら、残りHPが多い方の勝ちなのだ");
            ConfigurePanel(view, "directionDescriptionPanel", "hpDescriptionPanel", "DirectionDescription",
                "<b>左スティックを行きたい方向に倒したまま</b>\n<b>方向入力を続けた状態で</b>、リズムに合わせてコマンドを押すのだ！");
            var directionPanel = (GameObject)view.FindProperty("directionDescriptionPanel").objectReferenceValue;
            view.FindProperty("directionDescriptionText").objectReferenceValue = directionPanel.GetComponentsInChildren<TMP_Text>(true)
                .Single(label => label.name == "Text (TMP)");
            ConfigurePanel(view, "scoreDescriptionPanel", "hpDescriptionPanel", "ScoreDescription",
                "相手を倒すと、得点のカップを1つ獲得できるのだ！\nまずは相手を倒して、カップを手に入れるのだ！");
            ConfigurePanel(view, "defeatPracticePanel", "walkPracticePanel", "DefeatPractice",
                "これまで覚えたコマンドで、相手を倒そう！");
            ConfigurePanel(view, "scoreAwardDescriptionPanel", "hpDescriptionPanel", "ScoreAwardDescription",
                "相手を倒して、カップを1つ獲得したのだ！\n追加されたカップはここなのだ。これでチュートリアルは終了なのだ！");
            var pointer = (UnityEngine.UI.Image)view.FindProperty("barPointer").objectReferenceValue;
            pointer.transform.SetAsLastSibling();
        }

        static void ConfigurePanel(SerializedObject view, string property, string templateProperty, string name, string text) {
            var reference = view.FindProperty(property);
            var panel = (GameObject)reference.objectReferenceValue;
            if (panel == null) {
                var template = (GameObject)view.FindProperty(templateProperty).objectReferenceValue;
                panel = UnityEngine.Object.Instantiate(template, template.transform.parent);
                panel.name = name;
                reference.objectReferenceValue = panel;
            }
            panel.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Text (TMP)").text = text;
            panel.SetActive(false);
        }
    }
}
