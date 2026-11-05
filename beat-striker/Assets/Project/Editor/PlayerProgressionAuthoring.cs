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
    public static class PlayerProgressionAuthoring {
        const string Project = "Assets/Project/";
        const string DialogPath = Project + "App/UI/ChoiceDialog.prefab";
        const string RowPath = Project + "Screen/Ranking/RankingPlayerRowView.prefab";
        static readonly Color Ink = new Color32(8, 8, 10, 255), Yellow = new Color32(255, 220, 0, 255), Cyan = new Color32(0, 206, 224, 255);
        static TMP_FontAsset font;

        [MenuItem("Beat Striker/Preview/Show player confirmation")]
        public static void PreviewPlayerDialog() {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use the preview outside Play Mode.");
            var dialog = Object.FindFirstObjectByType<ChoiceDialogView>(FindObjectsInactive.Include);
            if (!dialog) throw new InvalidOperationException("Open the title or character selection scene first.");
            dialog.Show("1P プレイヤー確認", "ストライカー\nメゾフォルテ級", new[] { "OK", "編集", "戻る" });
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
        [MenuItem("Beat Striker/Preview/Hide player confirmation")]
        public static void HidePlayerDialog() {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use the preview outside Play Mode.");
            var dialog = Object.FindFirstObjectByType<ChoiceDialogView>(FindObjectsInactive.Include);
            if (dialog) dialog.Hide();
        }
        [MenuItem("Beat Striker/Preview/Show battle introduction")]
        public static void PreviewBattleIntroduction() {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use the preview outside Play Mode.");
            if (Object.FindFirstObjectByType<BattleTransitionView>()) return;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Project + "Transition/Battle/BattleTransition.prefab"));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Battle introduction preview"; root.hideFlags = HideFlags.DontSave;
            root.GetComponentInChildren<BattleTransitionView>(true).SetIdentities("プレイヤー\nメゾフォルテ級", "魔王\nフォルティッシモ級 / CPU Lv4");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
        [MenuItem("Beat Striker/Preview/Hide battle introduction")]
        public static void HideBattleIntroduction() {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use the preview outside Play Mode.");
            var view = Object.FindFirstObjectByType<BattleTransitionView>();
            if (view && view.transform.root.name == "Battle introduction preview") Object.DestroyImmediate(view.transform.root.gameObject);
        }

        [MenuItem("Beat Striker/Authoring/Player profiles and trial")]
        public static void Author() {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try {
                Open("Screen/Ranking/RankingScene.unity");
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Project + "Fonts/kaiso_up/Kaisotai-Next-UP-B SDF.asset");
                MakeDialogPrefab();
                EditPrefab("App/UI/ChoiceDialog.prefab", root => {
                    var canvas = root.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 2000;
                    if (!root.GetComponent<ModalInputBlocker>()) root.AddComponent<ModalInputBlocker>();
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) if (label.gameObject.name == "Label") label.fontStyle = FontStyles.Bold;
                });
                MakeRowPrefab(); AuthorRanking();
                Open("Screen/Title/TitleScene.unity"); AuthorTitle();
                Open("Screen/Menu/MenuScene.unity"); AuthorMenu();
                Open("Screen/CharacterSelect/SelectScene.unity"); AuthorCharacterSelect();
                EditPrefab("Battle/Battle.prefab", root => Set(root.GetComponent<BattleScope>(), "trialDialog", Dialog(root.transform)));
                EditPrefab("Transition/Battle/BattleTransition.prefab", root => {
                    var view = root.GetComponentInChildren<BattleTransitionView>(true);
                    var parent = view.GetComponentInChildren<Canvas>(true).transform;
                    var identities = parent.Find("Player identities") as RectTransform;
                    if (!identities) identities = Rect(parent,"Player identities",Vector2.zero,Vector2.one);
                    var group = identities.GetComponent<CanvasGroup>(); if (!group) group = identities.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 1; group.blocksRaycasts = false; group.interactable = false;
                    foreach (var side in new[]{"left","right"}) {
                        var label = Get(view,side+"IdentityLabel") as TMP_Text;
                        if (!label) label = Text(identities,side+" identity","",25,Vector2.zero,Vector2.one);
                        label.transform.SetParent(identities,false); label.name=side+" identity";
                        var rect=label.rectTransform; rect.anchorMin=new Vector2(side=="left"?.04f:.54f,.045f); rect.anchorMax=new Vector2(side=="left"?.46f:.96f,.22f); rect.offsetMin=rect.offsetMax=Vector2.zero;
                        label.font = font; label.fontSharedMaterial = font.material; label.enableAutoSizing=true; label.fontSizeMin=16; label.fontSizeMax=25; label.alignment=TextAlignmentOptions.Center;
                        Set(view,side+"IdentityLabel",label);
                    }
                    Set(view,"identityGroup",group);
                    var serialized = new SerializedObject(view); serialized.FindProperty("holdDuration").floatValue=1f; serialized.ApplyModifiedPropertiesWithoutUndo();
                });
                EditPrefab("App/App.prefab", AuthorApp);
                AssetDatabase.SaveAssets();
                RequireReferences(AssetDatabase.LoadAssetAtPath<GameObject>(Project+"App/App.prefab").GetComponent<AppScope>(), "trialSetting");
                RequireReferences(AssetDatabase.LoadAssetAtPath<GameObject>(Project+"Battle/Battle.prefab").GetComponent<BattleScope>(), "trialDialog");
                RequireReferences(AssetDatabase.LoadAssetAtPath<GameObject>(Project+"Transition/Battle/BattleTransition.prefab").GetComponentInChildren<BattleTransitionView>(true), "leftIdentityLabel", "rightIdentityLabel", "identityGroup");
                RequireReferences(AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath).GetComponent<ChoiceDialogView>(), "dialogRoot", "heading", "description", "input");
                var plugin=AssetImporter.GetAtPath(Project+"App/Replay/PlayerRecords.jslib") as PluginImporter;
                if (!plugin || !plugin.GetCompatibleWithPlatform(BuildTarget.WebGL)) throw new InvalidOperationException("Player records JavaScript must be imported for WebGL.");
                Debug.Log("[PlayerProgressionAuthoring] Saved player dialogs, grade list, trial configuration and scene references.");
            } finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        static void Open(string path) => EditorSceneManager.OpenScene(Project + path, OpenSceneMode.Single);
        static void RequireReferences(Object target, params string[] fields) {
            foreach(var field in fields) if(!Get(target,field)) throw new InvalidOperationException($"Missing {target.name}.{field}");
        }
        static Object Get(Object target, string property) => new SerializedObject(target).FindProperty(property).objectReferenceValue;
        static void Set(Object target, string property, Object value) {
            var serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }
        static void SetArray(Object target, string property, Object[] values) {
            var serialized = new SerializedObject(target); var array = serialized.FindProperty(property); array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }
        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max) {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        static TMP_Text Text(Transform parent, string name, string text, float size, Vector2 min, Vector2 max) {
            var label = Rect(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text;
            label.fontSize = size; label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false; label.richText = false; label.enableAutoSizing = true; label.fontSizeMin = size * .7f; label.fontSizeMax = size;
            return label;
        }
        static void Plate(RectTransform rect, Color color, float slant = 12) {
            var plate = rect.gameObject.AddComponent<RankingSlantedGraphic>(); plate.color = plate.endColor = color; plate.slant = slant;
        }
        static Botan Button(Transform parent, string name, string text, Vector2 min, Vector2 max, Color color) {
            var rect = Rect(parent, name, min, max); Plate(rect, color);
            var emitter = rect.gameObject.AddComponent<Botan>();
            var label = Text(rect, "Label", text, 25, new Vector2(.04f,.08f), new Vector2(.96f,.92f)); label.color = Ink;
            return emitter;
        }
        static void Height(GameObject target, float value) { var element = target.AddComponent<LayoutElement>(); element.preferredHeight = value; element.minHeight = value; }
        static VerticalLayoutGroup Vertical(GameObject target, int padding = 0) {
            var group = target.AddComponent<VerticalLayoutGroup>(); group.spacing = 8; group.padding = new RectOffset(padding,padding,padding,padding);
            group.childControlWidth = group.childControlHeight = true; group.childForceExpandHeight = false;
            target.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; return group;
        }
        static ScrollRect Scroll(Transform parent, string name, Vector2 min, Vector2 max) {
            var root = Rect(parent, name, min, max); var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect(root, "Viewport", Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,.08f); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", new Vector2(0,1), Vector2.one); content.pivot = new Vector2(.5f,1);
            Vertical(content.gameObject, 8); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.scrollSensitivity = 35;
            return scroll;
        }
        static void MakeDialogPrefab() {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath)) return;
            var root = Rect(null, "ChoiceDialog", Vector2.zero, Vector2.one);
            try {
                var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
                var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1000,562.5f); scaler.matchWidthOrHeight = 1;
                root.gameObject.AddComponent<GraphicRaycaster>(); root.gameObject.AddComponent<Image>().color = new Color(0,0,0,.86f);
                var panel = Rect(root, "Panel", new Vector2(.5f,.5f), new Vector2(.5f,.5f)); panel.sizeDelta = new Vector2(700,510); Plate(panel, Ink, 22);
                var edge = Rect(panel, "Accent", new Vector2(0,.98f), Vector2.one); Plate(edge, Yellow, 8);
                var heading = Text(panel, "Heading", "プレイヤー確認", 34, new Vector2(.06f,.85f), new Vector2(.94f,.97f)); heading.color = Yellow;
                var body = Text(panel, "Description", "名前と階級", 28, new Vector2(.08f,.65f), new Vector2(.92f,.84f));
                var inputRect = Rect(panel, "Name input", new Vector2(.08f,.54f), new Vector2(.92f,.64f)); inputRect.gameObject.AddComponent<Image>().color = Color.white;
                var input = inputRect.gameObject.AddComponent<TMP_InputField>(); var viewport = Rect(inputRect, "Text area", new Vector2(.025f,.08f), new Vector2(.975f,.92f)); viewport.gameObject.AddComponent<RectMask2D>();
                var inputText = (TextMeshProUGUI)Text(viewport, "Text", "", 27, Vector2.zero, Vector2.one); inputText.color = Ink; inputText.alignment = TextAlignmentOptions.MidlineLeft;
                input.textViewport = viewport; input.textComponent = inputText; input.targetGraphic = inputRect.GetComponent<Image>(); input.lineType = TMP_InputField.LineType.SingleLine; input.richText = false;
                var buttons = Rect(panel, "Choices", new Vector2(.12f,.05f), new Vector2(.88f,.50f));
                var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 7; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
                var emitters = new Object[5]; var labels = new Object[5];
                for (var i = 0; i < 5; i++) {
                    var emitter = Button(buttons, $"Choice {i + 1}", "決定", Vector2.zero, Vector2.one, i == 0 ? Yellow : Cyan);
                    Height(emitter.gameObject, 39); emitters[i] = emitter; labels[i] = emitter.GetComponentInChildren<TMP_Text>();
                }
                var view = root.gameObject.AddComponent<ChoiceDialogView>(); Set(view,"dialogRoot",root.gameObject); Set(view,"heading",heading); Set(view,"description",body); Set(view,"input",input); SetArray(view,"emitters",emitters); SetArray(view,"labels",labels);
                root.gameObject.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root.gameObject, DialogPath);
            } finally { Object.DestroyImmediate(root.gameObject); }
        }
        static ChoiceDialogView Dialog(Transform parent) {
            var existing = parent.GetComponentInChildren<ChoiceDialogView>(true); if (existing) return existing;
            return ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath), parent)).GetComponent<ChoiceDialogView>();
        }
        static void MakeRowPrefab() {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RowPath)) return;
            var root = Rect(null,"RankingPlayerRowView",Vector2.zero,Vector2.one);
            try {
                Plate(root, new Color32(28,34,46,255)); Height(root.gameObject,52);
                var emitter = root.gameObject.AddComponent<Botan>(); var name = Text(root,"Name","プレイヤー",25,new Vector2(.04f,.1f),new Vector2(.65f,.9f)); name.alignment = TextAlignmentOptions.MidlineLeft;
                var date = Text(root,"First acquisition","",14,new Vector2(.65f,.1f),new Vector2(.96f,.9f)); date.color = Cyan;
                var view = root.gameObject.AddComponent<RankingPlayerRowView>(); Set(view,"emitter",emitter); Set(view,"nameLabel",name); Set(view,"dateLabel",date);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject,RowPath);
            } finally { Object.DestroyImmediate(root.gameObject); }
        }
        public static void AuthorRanking() {
            var view = Object.FindFirstObjectByType<RankingPresenterView>();
            if (Get(view,"gradeScroll")) {
                ConfigureHistoryModal((GameObject)Get(view,"historyDialog"));
                EditorSceneManager.SaveScene(view.gameObject.scene); return;
            }
            var oldScroll = (ScrollRect)Get(view,"battleHistoryScrollRect"); var canvas = oldScroll.GetComponentInParent<Canvas>().transform;
            var dialog = Rect(canvas,"Player replay dialog",Vector2.zero,Vector2.one); dialog.gameObject.AddComponent<Image>().color = new Color(0,0,0,.96f);
            ConfigureHistoryModal(dialog.gameObject);
            var heading = Text(dialog,"Player heading","対戦履歴",30,new Vector2(.08f,.86f),new Vector2(.92f,.97f)); heading.color = Yellow;
            oldScroll.transform.SetParent(dialog,false); var rect=(RectTransform)oldScroll.transform; rect.anchorMin = new Vector2(.08f,.16f); rect.anchorMax = new Vector2(.92f,.83f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            ((Component)Get(view,"statusLabel")).transform.SetParent(dialog,false);
            foreach(var field in new[]{"battleHistoryScrollUpHoverBotan","battleHistoryScrollDownHoverBotan"}) ((Component)Get(view,field)).transform.SetParent(dialog,false);
            var close = Button(dialog,"Close","閉じる",new Vector2(.08f,.035f),new Vector2(.32f,.125f),Cyan);
            var more = Button(dialog,"More histories","さらに表示",new Vector2(.60f,.035f),new Vector2(.92f,.125f),Yellow);
            var gradeScroll=Scroll(canvas,"Grade list",new Vector2(.35f,.1f),new Vector2(.97f,.84f));
            var sections = new Object[6];
            for(var grade=0;grade<6;grade++) {
                var section=Rect(gradeScroll.content,$"Grade {grade}",new Vector2(0,1),Vector2.one); Vertical(section.gameObject,4);
                var title=Text(section,"Grade title",PlayerGrades.Label((PlayerGrade)grade),27,Vector2.zero,Vector2.one); title.color=Yellow; title.alignment=TextAlignmentOptions.MidlineLeft; Height(title.gameObject,44);
                var empty=Text(section,"Empty","まだ到達者がいません",17,Vector2.zero,Vector2.one); Height(empty.gameObject,32);
                var rows=Rect(section,"Players",new Vector2(0,1),Vector2.one); Vertical(rows.gameObject);
                var load=Button(section,"More players","さらに表示",Vector2.zero,Vector2.one,Cyan); Height(load.gameObject,36); load.gameObject.SetActive(false);
                var component=section.gameObject.AddComponent<RankingGradeSectionView>(); Set(component,"heading",title); Set(component,"emptyLabel",empty.gameObject); Set(component,"rowsParent",rows); Set(component,"rowPrefab",AssetDatabase.LoadAssetAtPath<RankingPlayerRowView>(RowPath)); Set(component,"moreEmitter",load);
                var so=new SerializedObject(component); so.FindProperty("grade").intValue=grade; so.ApplyModifiedPropertiesWithoutUndo(); sections[grade]=component;
            }
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true)) {
                if(text.gameObject.name=="対戦履歴") text.text="階級リスト";
                if(text.gameObject.name=="History Limit") text.text="初回獲得順";
                if(text.gameObject.name=="Replay Help") text.text="プレイヤーを選んで対戦履歴を表示";
                if(text.gameObject.name=="Replay Invitation") text.text="試練に挑み、頂点へ！";
            }
            var status=Text(canvas,"Ranking status","",16,new Vector2(.35f,.015f),new Vector2(.97f,.075f));
            Set(view,"historyList",Object.FindFirstObjectByType<RankingHistoryListView>()); SetArray(view,"gradeSections",sections); Set(view,"historyDialog",dialog.gameObject); Set(view,"historyHeading",heading); Set(view,"rankingStatus",status); Set(view,"closeHistoryEmitter",close); Set(view,"moreHistoryEmitter",more); Set(view,"gradeScroll",gradeScroll);
            dialog.gameObject.SetActive(false); EditorSceneManager.SaveScene(view.gameObject.scene);
        }
        static void ConfigureHistoryModal(GameObject root) {
            if (!root.GetComponent<ModalInputBlocker>()) root.AddComponent<ModalInputBlocker>();
            var canvas = root.GetComponent<Canvas>(); if (!canvas) canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 2000;
            if (!root.GetComponent<GraphicRaycaster>()) root.AddComponent<GraphicRaycaster>();
        }
        static void AuthorTitle() {
            var view=Object.FindFirstObjectByType<TitleScene>();
            var canvas=((Component)Get(view,"settingsEmitter")).GetComponentInParent<Canvas>();
            if(!Get(view,"onlineEmitter")) {
                var online=Button(canvas.transform,"Online battle","オンライン",new Vector2(.72f,.075f),new Vector2(.96f,.18f),Cyan); Set(view,"onlineEmitter",online);
            }
            var onlineRect=(RectTransform)((Component)Get(view,"onlineEmitter")).transform;
            onlineRect.SetParent(canvas.transform,false); onlineRect.anchorMin=new Vector2(.72f,.075f); onlineRect.anchorMax=new Vector2(.96f,.18f); onlineRect.offsetMin=onlineRect.offsetMax=Vector2.zero;
            Set(view,"playerDialog",Dialog(view.transform)); EditorSceneManager.SaveScene(view.gameObject.scene);
        }
        static void AuthorMenu() {
            var view=Object.FindFirstObjectByType<MenuPresenterView>(); var button=(Component)Get(view,"trialEmitter");
            foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)) {
                text.font = font; text.fontSharedMaterial = font.material;
                var serialized = new SerializedObject(text); serialized.FindProperty("m_text").stringValue = "試練";
                serialized.FindProperty("m_fontStyle").intValue=(int)FontStyles.Bold; serialized.FindProperty("m_fontWeight").intValue=700; serialized.FindProperty("m_fontColor").colorValue=Color.white; serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            var icon=button.GetComponentsInChildren<RawImage>(true).First(image=>image.gameObject.name=="Chara");
            var imageData=new SerializedObject(icon); imageData.FindProperty("m_Texture").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Texture2D>(Project+"Screen/CharacterSelect/1761937629928.png"); imageData.FindProperty("m_Color").colorValue=Color.white; imageData.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
            button.gameObject.name="Trial button"; EditorSceneManager.SaveScene(view.gameObject.scene);
        }
        static void AuthorCharacterSelect() {
            var view=Object.FindFirstObjectByType<SelectScene>(); Set(view,"cpuDialog",Dialog(view.transform)); EditorSceneManager.SaveScene(view.gameObject.scene);
        }
        static void EditPrefab(string path, Action<GameObject> edit) {
            var root=PrefabUtility.LoadPrefabContents(Project+path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root,Project+path); } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void AuthorApp(GameObject root) {
            var cursorFactory = root.GetComponent<CursorFactory>();
            if (!cursorFactory) cursorFactory = root.GetComponentInChildren<CursorFactory>(true);
            var cursorParent = (RectTransform)Get(cursorFactory, "cursorParent");
            var cursorCanvas = cursorParent.GetComponent<Canvas>();
            if (!cursorCanvas) cursorCanvas = cursorParent.gameObject.AddComponent<Canvas>();
            cursorCanvas.overrideSorting = true; cursorCanvas.sortingOrder = 3000;
            var scope=root.GetComponent<AppScope>(); var trial=root.GetComponent<TrialSetting>(); if(!trial) trial=root.AddComponent<TrialSetting>(); Set(scope,"trialSetting",trial);
            var registry=root.GetComponent<AIRegistry>(); var so=new SerializedObject(registry); var entries=so.FindProperty("entries");
            var definitions=new[]{("Hero","HeroTactical",AiStrikerPattern.Hero),("Wizard","WizardTactical",AiStrikerPattern.Wizard),("Warrior","WarriorTactical",AiStrikerPattern.Warrior),("DemonKing","Mock",AiStrikerPattern.DemonKing)};
            var index=1; entries.arraySize=17;
            foreach(var definition in definitions) for(var level=1;level<=4;level++) {
                var entry=entries.GetArrayElementAtIndex(index++); entry.FindPropertyRelative("Id").stringValue=$"{definition.Item1}.Lv{level}";
                entry.FindPropertyRelative("BrainPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AiBrain>(Project+$"Ai/Models/{definition.Item2}.prefab");
                entry.FindPropertyRelative("Strength").intValue=level; entry.FindPropertyRelative("Self").enumValueIndex=(int)definition.Item3; entry.FindPropertyRelative("Opponent").enumValueIndex=0;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var factory=root.GetComponent<AppTransitionFactory>(); if(!factory) factory=root.GetComponentInChildren<AppTransitionFactory>(true);
            var serialized=new SerializedObject(factory); var transitions=serialized.FindProperty("transitionAnimationEntries");
            var prefab=AssetDatabase.LoadAssetAtPath<BattleTransitionView>(Project+"Transition/Battle/BattleTransition.prefab");
            foreach(var from in new[]{AppScene.Live,AppScene.Street,AppScene.Ranking}) foreach(var to in new[]{AppScene.Live,AppScene.Street}) {
                var found=false; for(var i=0;i<transitions.arraySize;i++){var entry=transitions.GetArrayElementAtIndex(i);if(entry.FindPropertyRelative("FromScene").enumValueIndex==(int)from&&entry.FindPropertyRelative("ToScene").enumValueIndex==(int)to) found=true;}
                if(found) continue; var next=transitions.arraySize; transitions.arraySize++; var item=transitions.GetArrayElementAtIndex(next); item.FindPropertyRelative("FromScene").enumValueIndex=(int)from; item.FindPropertyRelative("ToScene").enumValueIndex=(int)to; item.FindPropertyRelative("Prefab").objectReferenceValue=prefab;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
