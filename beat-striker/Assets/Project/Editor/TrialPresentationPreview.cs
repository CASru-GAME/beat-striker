using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VContainer;
using Object = UnityEngine.Object;

namespace Alice.Editor {
    public static class TrialPresentationPreview {
        const string Project = "Assets/Project/";
        static Object Get(Object owner, string field) => new SerializedObject(owner).FindProperty(field).objectReferenceValue;

        [MenuItem("Beat Striker/Preview/Challenge/Play promotion and check input lock")]
        public static async void PlayPromotion() {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Start the application in Play Mode first.");
            var app = Object.FindFirstObjectByType<AppScope>();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Project + "Battle/UI/View/TrialResultDialog.prefab"));
            var view = root.GetComponent<ChoiceDialogView>();
            var result = root.GetComponent<TrialResultView>();
            using var presenter = new ChoiceDialogPresenter(view, app.Container.Resolve<IGamePadRegistry>(), app.Container.Resolve<IInputFocusService>());
            try {
                var presentation = new TrialResultPresentation(true, 3, 4, 1);
                var pending = presenter.AskAsync(presentation.Title, "プレビュー", new[] { "続けて挑戦", "一旦終わる" }, inputPlayerId: 0, directInput: true);
                result.Play(presentation);
                var button = (Component)new SerializedObject(view).FindProperty("emitters").GetArrayElementAtIndex(0).objectReferenceValue;
                var start = Time.unscaledTime;
                var attempts = 0;
                while (!view.IsInteractable) {
                    ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
                    attempts++;
                    if (pending.IsCompleted) throw new InvalidOperationException("The result accepted input before its presentation finished.");
                    if (Time.unscaledTime - start > 6) throw new InvalidOperationException("The result did not unlock its actions.");
                    await Awaitable.NextFrameAsync(result.destroyCancellationToken);
                }
                SaveRuntimeFrame();
                ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
                var choice = await pending;
                Debug.Log($"[TrialPreview] Live promotion finished; {attempts} early clicks blocked; final choice={choice}. No player progression was changed.");
            } catch (OperationCanceledException) { }
            finally { if (root) Object.Destroy(root); }
        }
        static void SaveRuntimeFrame() {
            Directory.CreateDirectory("Temp/ChallengePreview");
            ScreenCapture.CaptureScreenshot("Temp/ChallengePreview/runtime-promotion.png");
        }

        [MenuItem("Beat Striker/Preview/Challenge/Capture presentations")]
        public static void Capture() {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Stop Play Mode and save the scene before previewing.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try {
                CaptureRanking();
                CaptureResult("promotion-success", new TrialResultPresentation(true, 3, 4, 1), 2.8f);
                CaptureResult("promotion-spin", new TrialResultPresentation(true, 3, 4, 1), .72f);
                CaptureResult("promotion-burst", new TrialResultPresentation(true, 3, 4, 1), 1.8f);
                CaptureResult("promotion-failure", new TrialResultPresentation(false, 3, 4, 1), 1.5f);
                CaptureResult("failure-stamp", new TrialResultPresentation(false, 3, 4, 1), .52f);
                CaptureResult("streak-success", new TrialResultPresentation(true, 5, -1, 7), 2.8f);
                CaptureResult("streak-failure", new TrialResultPresentation(false, 5, -1, 7), 1.5f);
                CaptureIntroduction();
                CaptureAnnouncement();
                Debug.Log("[TrialPreview] Captured serialized challenge UI and animation samples in Temp/ChallengePreview.");
            } finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        static Camera Camera() {
            var camera = new GameObject("Preview camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 360;
            camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(6, 10, 18, 255);
            return camera;
        }
        static void SetupCanvas(GameObject root, Camera camera) {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 500;
            }
        }
        static void Save(Camera camera, string name) {
            Directory.CreateDirectory("Temp/ChallengePreview");
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes($"Temp/ChallengePreview/{name}.png", texture.EncodeToPNG());
            } finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(texture); }
        }
        static GameObject Dialog(string path, string title, string body, string first, string second = null) {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Project + path));
            root.SetActive(true);
            var dialog = root.GetComponent<ChoiceDialogView>();
            ((TMP_Text)Get(dialog, "heading")).text = title;
            ((TMP_Text)Get(dialog, "description")).text = body;
            foreach (var field in new[] { "gradedDescription", "gradeView", "selectedGradeView", "input", "previousValue", "nextValue" })
                ((Component)Get(dialog, field)).gameObject.SetActive(false);
            ((GameObject)Get(dialog, "inputGuide")).SetActive(false);
            var so = new SerializedObject(dialog);
            var emitters = so.FindProperty("emitters"); var labels = so.FindProperty("labels"); var keys = so.FindProperty("keyboardKeys");
            for (var i = 0; i < emitters.arraySize; i++) {
                ((Component)emitters.GetArrayElementAtIndex(i).objectReferenceValue).gameObject.SetActive(i == 0 || i == 1 && second != null);
                ((TMP_Text)labels.GetArrayElementAtIndex(i).objectReferenceValue).text = i == 0 ? first : second ?? "";
                ((GameObject)keys.GetArrayElementAtIndex(i).objectReferenceValue).SetActive(false);
            }
            return root;
        }
        static void CaptureResult(string name, TrialResultPresentation value, float time) {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = Camera();
            var root = Dialog("Battle/UI/View/TrialResultDialog.prefab", value.Title, "プレイヤー", "続けて挑戦", "一旦終わる");
            root.GetComponent<TrialResultView>().Preview(value, time); SetupCanvas(root, camera); Save(camera, name);
        }
        static void CaptureIntroduction() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = Camera();
            var root = Dialog("Screen/CharacterSelect/TrialIntroductionDialog.prefab", "チャレンジモードとは", "CPUと戦い、階級の頂点を目指すモード！\n最初はピアニッシモ級からスタート\n好きなストライカーで挑戦しよう", "挑戦する！");
            SetupCanvas(root, camera); Save(camera, "introduction");
        }
        static void CaptureAnnouncement() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = Camera();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Project + "Transition/Battle/BattleTransition.prefab"));
            root.SetActive(true); root.GetComponentInChildren<TrialAnnouncementView>(true).Preview(4, .6f);
            SetupCanvas(root, camera); Save(camera, "announcement");
        }
        static void CaptureRanking() {
            var scene = EditorSceneManager.OpenScene(Project + "Screen/Ranking/RankingScene.unity");
            var camera = Camera();
            foreach (var root in scene.GetRootGameObjects()) SetupCanvas(root, camera);
            Save(camera, "ranking-reference");
        }

        [MenuItem("Beat Striker/Preview/Challenge/Inspect saved references")]
        public static void InspectReferences() {
            var paths = new[] { "Battle/UI/View/TrialResultDialog.prefab", "Screen/CharacterSelect/TrialIntroductionDialog.prefab", "Transition/Battle/BattleTransition.prefab", "Battle/Battle.prefab" };
            foreach (var path in paths) {
                var root = PrefabUtility.LoadPrefabContents(Project + path);
                try {
                    foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c is TrialResultView || c is TrialIntroductionView || c is TrialAnnouncementView)) {
                        var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                        while (property.NextVisible(true))
                            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
                                throw new InvalidOperationException($"Missing {path}: {component.GetType().Name}.{property.propertyPath}");
                    }
                    Debug.Log($"[TrialPreview] Reopened and inspected {path}");
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
