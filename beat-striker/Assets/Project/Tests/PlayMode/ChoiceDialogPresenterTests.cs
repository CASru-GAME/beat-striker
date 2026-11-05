#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alice;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using TMPro;
using Object = UnityEngine.Object;

public class ChoiceDialogPresenterTests {
    const string DialogPath = "Assets/Project/App/UI/ChoiceDialog.prefab";
    GameObject owner;
    GameObject eventSystemOwner;

    [TearDown]
    public void TearDown() {
        if (owner) Object.DestroyImmediate(owner);
        if (eventSystemOwner) Object.DestroyImmediate(eventSystemOwner);
    }

    [UnityTest]
    public IEnumerator CpuChoicesFinishAfterTheClickHasFinishedDispatching() {
        eventSystemOwner = new GameObject("Dialog test events", typeof(EventSystem));
        var eventSystem = eventSystemOwner.GetComponent<EventSystem>();
        owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath));
        var view = owner.GetComponentInChildren<ChoiceDialogView>(true);
        using var pads = new CountingPads();
        using var focus = new InputFocusService();
        using var presenter = new ChoiceDialogPresenter(view, pads, focus);
        var emitters = (ActionEmitter[])typeof(ChoiceDialogView).GetField("emitters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        for (var choice = 0; choice < 6; choice++) {
            var pending = presenter.AskAsync("CPUの強さ", "", new[] { "そのまま", "Lv1", "Lv2", "Lv3", "Lv4", "戻る" });
            bool callerResumed = false;
            async Task ResumeCaller() { await pending; callerResumed = true; }
            var continuation = ResumeCaller();
            yield return new WaitUntil(() => view.IsInteractable);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)emitters[choice].transform;
            var pointer = new PointerEventData(eventSystem) {
                pointerId = 0,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            var target = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject);
            Assert.That(target, Is.EqualTo(emitters[choice].gameObject));
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            Assert.That(pending.IsCompleted, Is.False, "The dialog must finish closing before completing the choice.");
            Assert.That(callerResumed, Is.False, "The scene must remain in its dialog state until all subscribers finish handling this click.");
            yield return new WaitUntil(() => continuation.IsCompleted);
            Assert.That(pending.Result, Is.EqualTo(choice));
        }
    }

    [UnityTest]
    public IEnumerator EndingTextEntryReleasesKeyboardAndAllowsConfirmButton() {
        eventSystemOwner = new GameObject("Dialog test events", typeof(EventSystem));
        var eventSystem = eventSystemOwner.GetComponent<EventSystem>();
        owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath));
        var view = owner.GetComponentInChildren<ChoiceDialogView>(true);
        var input = owner.GetComponentInChildren<TMP_InputField>(true);
        using var pads = new CountingPads();
        using var focus = new InputFocusService();
        using var presenter = new ChoiceDialogPresenter(view, pads, focus);
        var pending = presenter.AskAsync("名前", "入力", new[] { "決定", "戻る" }, input: "");
        yield return new WaitUntil(() => view.IsInteractable);
        Assert.That(input.isFocused, Is.False);
        Assert.That(input.onFocusSelectAll, Is.False);
        eventSystem.SetSelectedGameObject(input.gameObject);
        yield return null;
        Assert.That(focus.HasTextFocus, Is.True);
        input.text = "テスト";
        input.OnSubmit(new BaseEventData(eventSystem));
        Assert.That(focus.HasTextFocus, Is.False, "Enter must release keyboard control even without a separate deselect event.");
        Assert.That(pending.IsCompleted, Is.False, "Ending text entry must leave the dialog available for button confirmation.");
        yield return null;
        Assert.That(input.isFocused, Is.False);
        var emitters = (ActionEmitter[])typeof(ChoiceDialogView).GetField("emitters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        ExecuteEvents.Execute(emitters[0].gameObject, new PointerEventData(eventSystem) { pointerId = 0 }, ExecuteEvents.pointerDownHandler);
        Assert.That(pending.IsCompleted, Is.False);
        yield return new WaitUntil(() => pending.IsCompleted);
        Assert.That(pending.Result, Is.Zero);
        Assert.That(presenter.InputText, Is.EqualTo("テスト"));

        var next = presenter.AskAsync("確認", "テスト", new[] { "OK" });
        yield return new WaitUntil(() => view.IsInteractable);
        Assert.That(focus.HasTextFocus, Is.False);
        ExecuteEvents.Execute(emitters[0].gameObject, new PointerEventData(eventSystem) { pointerId = 0 }, ExecuteEvents.pointerDownHandler);
        yield return new WaitUntil(() => next.IsCompleted);
        Assert.That(next.Result, Is.Zero);
    }

    [UnityTest]
    public IEnumerator DestroyingViewCancelsPendingDialogAndReleasesAppInputSubscriptions() {
        owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath));
        var view = owner.GetComponentInChildren<ChoiceDialogView>(true);
        using var pads = new CountingPads();
        using var focus = new InputFocusService();
        using var presenter = new ChoiceDialogPresenter(view, pads, focus);
        var pending = presenter.AskAsync("名前", "入力", new[] { "OK" }, input: "プレイヤー");
        Assert.That(focus.HasTextFocus, Is.False, "Opening the dialog must not focus the name field or block keyboard buttons.");
        Assert.That(pads.ActiveSubscriptions, Is.EqualTo(1));

        Object.Destroy(owner);
        yield return null;
        Assert.That(pending.IsCanceled, Is.True);
        Assert.That(focus.HasTextFocus, Is.False);
        Assert.That(pads.ActiveSubscriptions, Is.Zero, "A destroyed scene must not retain any subscriptions to the app's controller registry.");
        presenter.Dispose();
        Assert.That(pads.ActiveSubscriptions, Is.Zero, "Explicit disposal after view destruction must remain safe.");
        Assert.Throws<ObjectDisposedException>(() => presenter.AskAsync("閉じた画面", "", new[] { "OK" }));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ShowFailureRestoresNameInputStateAndAllowsAnotherDialog() {
        owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath));
        var view = owner.GetComponentInChildren<ChoiceDialogView>(true);
        using var pads = new CountingPads();
        using var focus = new InputFocusService();
        using var presenter = new ChoiceDialogPresenter(view, pads, focus);
        var heading = typeof(ChoiceDialogView).GetField("heading", BindingFlags.Instance | BindingFlags.NonPublic);
        var originalHeading = heading.GetValue(view);
        heading.SetValue(view, null);
        try {
            Assert.Throws<NullReferenceException>(() => presenter.AskAsync("名前", "入力", new[] { "OK" }, input: "プレイヤー"));
            Assert.That(focus.HasTextFocus, Is.False, "A failure while showing the dialog must restore normal keyboard input.");
        } finally {
            heading.SetValue(view, originalHeading);
        }

        var pending = presenter.AskAsync("再試行", "", new[] { "OK" });
        Assert.That(pending.IsCompleted, Is.False, "The failed request must not leave the presenter stuck in its open state.");
        presenter.Dispose();
        Assert.That(pending.IsCanceled, Is.True);
        Assert.That(pads.ActiveSubscriptions, Is.Zero);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator NameDialogUsesGuestPlaceholderAndFixedConfirmCancelButtons() {
        owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath));
        var view = owner.GetComponentInChildren<ChoiceDialogView>(true);
        var field = owner.GetComponentInChildren<TMP_InputField>(true);
        using var pads = new CountingPads();
        using var focus = new InputFocusService();
        using var presenter = new ChoiceDialogPresenter(view, pads, focus);
        var pending = presenter.AskAsync("名前変更", "", new[] { "確定", "戻る" }, "", inputPlayerId: 0, directInput: true);
        yield return new WaitUntil(() => view.IsInteractable);
        Assert.That(((TMP_Text)field.placeholder).text, Is.EqualTo("ゲスト"));
        Assert.That(PlayerProfileService.EnteredNameOrGuest(field.text), Is.EqualTo("ゲスト"));
        yield return null;
        pads.Press(GamePadButton.East);
        yield return new WaitUntil(() => pending.IsCompleted);
        Assert.That(pending.Result, Is.Zero);
        pending = presenter.AskAsync("名前変更", "", new[] { "確定", "戻る" }, "", inputPlayerId: 0, directInput: true);
        yield return new WaitUntil(() => view.IsInteractable);
        pads.Press(GamePadButton.South);
        yield return new WaitUntil(() => pending.IsCompleted);
        Assert.That(pending.Result, Is.EqualTo(1));
        pending = presenter.AskAsync("CPUの強さ", "Lv2", new[] { "そのまま", "Lv1", "Lv2", "Lv3", "Lv4", "戻る" }, directInput: true);
        yield return new WaitUntil(() => view.IsInteractable);
        pads.Press(GamePadButton.East);
        yield return new WaitUntil(() => pending.IsCompleted);
        Assert.That(pending.Result, Is.Zero, "A initially keeps the current CPU level.");
    }

    [UnityTest]
    public IEnumerator KeyboardGuidesUseSquareKeysAndControllerGuidesUseSprites() {
        owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath));
        var view = owner.GetComponentInChildren<ChoiceDialogView>(true);
        using var pads = new CountingPads { DeviceName = "Keybord" };
        using var focus = new InputFocusService();
        using var presenter = new ChoiceDialogPresenter(view, pads, focus);
        var pending = presenter.AskAsync("名前変更", "", new[] { "確定", "戻る" }, "", directInput: true);
        yield return new WaitUntil(() => view.IsInteractable);
        var keys = (GameObject[])typeof(ChoiceDialogView).GetField("keyboardKeys", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        var keyLabels = (TMP_Text[])typeof(ChoiceDialogView).GetField("keyboardKeyLabels", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        var labels = (TMP_Text[])typeof(ChoiceDialogView).GetField("labels", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        Assert.That(keys[0].activeSelf, Is.True);
        Assert.That(keys[1].activeSelf, Is.True);
        Assert.That(keyLabels[0].text, Is.EqualTo("L"));
        Assert.That(keyLabels[1].text, Is.EqualTo("K"));
        Assert.That(labels[0].text, Is.EqualTo("確定"));
        yield return null;
        pads.Press(GamePadButton.East);
        yield return new WaitUntil(() => pending.IsCompleted);
        pending = presenter.AskAsync("CPUの強さ", "前回の設定 Lv2", new[] { "そのまま", "Lv1", "Lv2", "Lv3", "Lv4", "戻る" }, directInput: true);
        yield return new WaitUntil(() => view.IsInteractable);
        Assert.That(keys[0].activeSelf, Is.True);
        Assert.That(keys[5].activeSelf, Is.True);
        for (var index = 1; index <= 4; index++) {
            Assert.That(keys[index].activeSelf, Is.False, "CPU levels are pointer choices, without duplicate shortcuts.");
            Assert.That(labels[index].text, Is.EqualTo($"Lv{index}"));
        }
        var emitters = (ActionEmitter[])typeof(ChoiceDialogView).GetField("emitters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        Canvas.ForceUpdateCanvases();
        var keepButton = (RectTransform)emitters[0].transform;
        var keyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(keepButton, keys[0].transform);
        Assert.That(keyBounds.min.x, Is.GreaterThanOrEqualTo(keepButton.rect.xMin + 14f), "The key must stay inside the hexagon's pointed edge.");
        Assert.That(keyBounds.max.x, Is.LessThanOrEqualTo(keepButton.rect.xMax - 14f));
        labels[0].ForceMeshUpdate();
        Assert.That(labels[0].textBounds.size.x, Is.LessThanOrEqualTo(labels[0].rectTransform.rect.width + .5f), "The keep-current label must fit beside its key.");
        yield return null;
        var pointer = new PointerEventData(EventSystem.current) { pointerId = 0 };
        ExecuteEvents.Execute(emitters[3].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        Assert.That(keys[0].activeSelf, Is.True, "Hover must not move the default action's key guide.");
        Assert.That(keys[3].activeSelf, Is.False);
        ExecuteEvents.Execute(emitters[3].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        pads.Press(GamePadButton.East);
        yield return new WaitUntil(() => pending.IsCompleted);
        Assert.That(pending.Result, Is.EqualTo(3), "A cursor click must win over the default-action shortcut from the same physical input.");
        pads.DeviceName = "Test controller";
        pending = presenter.AskAsync("名前変更", "", new[] { "確定", "戻る" }, "", directInput: true);
        yield return new WaitUntil(() => view.IsInteractable);
        Assert.That(keys[0].activeSelf, Is.False);
        Assert.That(keys[1].activeSelf, Is.False);
        Assert.That(labels[0].text, Does.Contain("<sprite"));
        pads.Press(GamePadButton.South);
        yield return new WaitUntil(() => pending.IsCompleted);
    }

    class CountingPads : IGamePadRegistry, IDisposable {
        readonly Pad[] pads;
        public int ActiveSubscriptions { get; private set; }
        public string DeviceName { get; set; } = "Test controller";
        public CountingPads() => pads = new[] { new Pad(0, this), new Pad(1, this), new Pad(2, this), new Pad(3, this) };
        readonly Subject<PlayerGamePadButtonEvent> buttons = new();
        public void Press(GamePadButton button) => buttons.OnNext(new PlayerGamePadButtonEvent(0, button));
        public Observable<PlayerGamePadButtonEvent> OnAnyButtonDown => Observable.Create<PlayerGamePadButtonEvent>(observer => {
            ActiveSubscriptions++;
            var subscription = buttons.Subscribe(observer.OnNext);
            return Disposable.Create(() => { subscription.Dispose(); ActiveSubscriptions--; });
        });
        public IPlayerGamePad Get(int playerId) => pads[playerId];
        Observable<T> Track<T>() => Observable.Create<T>(_ => {
            ActiveSubscriptions++;
            return Disposable.Create(() => ActiveSubscriptions--);
        });
        public void Dispose() { foreach (var pad in pads) pad.Dispose(); buttons.Dispose(); }
        public IPlayerGamePad RequestRegister(IGamePad gamePad) => throw new NotSupportedException();
        public IPlayerGamePad RequestRegister(int playerId, IGamePad gamePad) => throw new NotSupportedException();
        public IPlayerGamePad RequestRegisterReplay(int playerId, IGamePad gamePad) => throw new NotSupportedException();
        public IPlayerGamePad RequestRegisterLowPriority(int playerId, IGamePad gamePad) => throw new NotSupportedException();
        public void RequestRegisterBattleOverride(int playerId, IGamePad gamePad) => throw new NotSupportedException();
        public void ClearBattleOverride(int playerId) => throw new NotSupportedException();
        public void RestoreReplayOverrides() => throw new NotSupportedException();
        public void RequestUnregister(int playerId) => throw new NotSupportedException();
        public void RequestUnregister(IGamePad gamePad) => throw new NotSupportedException();
        public void RestoreOfflinePrimaryLayout(int localOnlinePlayerId) => throw new NotSupportedException();
        public void HandlePlayerSlotClick(int sourcePlayerId, int targetPlayerId) => throw new NotSupportedException();
        public GamePadButton GetPhysicalFaceButton(GamePadButton button) => button;

        class Pad : IPlayerGamePad, IDisposable {
            readonly CountingPads owner;
            readonly ReactiveProperty<bool> hasGamePad = new(false);
            public int PlayerId { get; }
            public string DeviceName => owner.DeviceName;
            public ReadOnlyReactiveProperty<bool> HasGamePad => hasGamePad;
            public Observable<Vector2> OnDirection => owner.Track<Vector2>();
            public Observable<Vector2> OnCursorDirection => owner.Track<Vector2>();
            public Observable<Unit> OnDirectionCanceled => owner.Track<Unit>();
            public Observable<GamePadButton> OnButtonDown => Observable.Empty<GamePadButton>();
            public Observable<GamePadButton> OnButtonUp => Observable.Empty<GamePadButton>();
            public Pad(int playerId, CountingPads owner) { PlayerId = playerId; this.owner = owner; }
            public void Dispose() => hasGamePad.Dispose();
        }
    }
}
#endif
