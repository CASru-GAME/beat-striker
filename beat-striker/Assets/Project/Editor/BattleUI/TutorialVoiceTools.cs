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
            public string DisplayCharacters;
        }
        [Serializable] record Clip {
            public string Id;
            public string Text;
        }
        [Serializable] record Line {
            public string Key;
            public string[] Segments;
            public TutorialLessonPage[] Pages;
        }

        [MenuItem("Tools/Battle/Configure Tutorial Narration")]
        public static void Configure() {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ROOT + "/voice-lines.json"));
            EnsureDisplayCharacters(manifest.DisplayCharacters);
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
                serializedView.FindProperty("rhythmPracticeFormat").stringValue = "{2}のどれかを、ビートに合わせて3回連続成功させよう";
                ConfigureEndingPanels(root, serializedView, manifest);
                ConfigurePanel(serializedView, "overviewPanel", "hpDescriptionPanel", "Overview", FirstPage(manifest, "Overview"));
                ConfigurePanel(serializedView, "hpDescriptionPanel", "hpDescriptionPanel", "HpDescription", FirstPage(manifest, "HpDescription"));
                ConfigurePanel(serializedView, "spDescriptionPanel", "hpDescriptionPanel", "SpDescription", FirstPage(manifest, "SpDescription"));
                ConfigurePanel(serializedView, "walkDescriptionPanel", "hpDescriptionPanel", "WalkDescription", FirstPage(manifest, "WalkDescription"));
                ConfigureDescriptionPages(serializedView, manifest);
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

        static void EnsureDisplayCharacters(string characters) {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/kaiso_up/Kaisotai-Next-UP-B SDF.asset");
            var serializedFont = new SerializedObject(font);
            serializedFont.FindProperty("m_SourceFontFile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/kaiso_up/Kaisotai-Next-UP-B.ttf");
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
            var population = font.atlasPopulationMode;
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = true;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Tutorial characters are missing from the source font: " + missing);
            font.atlasPopulationMode = population;
            foreach (var texture in font.atlasTextures) {
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture))) AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(texture);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }

        static void ConfigureDescriptionPages(SerializedObject view, Manifest manifest) {
            var groups = manifest.Lines.Where(line => line.Pages != null && line.Pages.Length > 0).ToArray();
            var serializedGroups = view.FindProperty("descriptionPages");
            serializedGroups.arraySize = groups.Length;
            for (var i = 0; i < groups.Length; i++) {
                var line = groups[i];
                var property = char.ToLowerInvariant(line.Key[0]) + line.Key.Substring(1) + "Panel";
                var panel = (GameObject)view.FindProperty(property).objectReferenceValue;
                var text = panel.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Text (TMP)");
                text.text = line.Pages[0].Text;
                var group = serializedGroups.GetArrayElementAtIndex(i);
                group.FindPropertyRelative("Key").intValue = (int)Enum.Parse<TutorialUiKey>(line.Key);
                group.FindPropertyRelative("Text").objectReferenceValue = text;
                var pages = group.FindPropertyRelative("Pages");
                pages.arraySize = line.Pages.Length;
                for (var j = 0; j < line.Pages.Length; j++) {
                    var page = pages.GetArrayElementAtIndex(j);
                    page.FindPropertyRelative("Text").stringValue = line.Pages[j].Text;
                    page.FindPropertyRelative("Voice").stringValue = line.Pages[j].Voice;
                }
            }
        }
        static string FirstPage(Manifest manifest, string key) => manifest.Lines.Single(line => line.Key == key).Pages[0].Text;

        static void ConfigureEndingPanels(GameObject root, SerializedObject view, Manifest manifest) {
            var battleView = root.GetComponentsInChildren<BattlePresenterView>(true).Single();
            var serializedBattleView = new SerializedObject(battleView);
            view.FindProperty("battleView").objectReferenceValue = battleView;
            var timerText = (TMP_Text)serializedBattleView.FindProperty("remainingBeatCountText").objectReferenceValue;
            view.FindProperty("timerPointerTarget").objectReferenceValue = timerText.rectTransform;
            ConfigurePanel(view, "timerDescriptionPanel", "hpDescriptionPanel", "TimerDescription",
                FirstPage(manifest, "TimerDescription"));
            var timerPanel = (GameObject)view.FindProperty("timerDescriptionPanel").objectReferenceValue;
            ((RectTransform)timerPanel.transform).anchoredPosition = new Vector2(0f, -90f);
            ConfigurePanel(view, "victoryDescriptionPanel", "hpDescriptionPanel", "VictoryDescription",
                FirstPage(manifest, "VictoryDescription"));
            ConfigurePanel(view, "directionDescriptionPanel", "hpDescriptionPanel", "DirectionDescription",
                FirstPage(manifest, "DirectionDescription"));
            ConfigurePanel(view, "scoreDescriptionPanel", "hpDescriptionPanel", "ScoreDescription",
                FirstPage(manifest, "ScoreDescription"));
            ConfigurePanel(view, "defeatPracticePanel", "walkPracticePanel", "DefeatPractice",
                "覚えた技を使って、相手を倒すのだ！");
            ConfigurePanel(view, "scoreAwardDescriptionPanel", "hpDescriptionPanel", "ScoreAwardDescription",
                FirstPage(manifest, "ScoreAwardDescription"));
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
