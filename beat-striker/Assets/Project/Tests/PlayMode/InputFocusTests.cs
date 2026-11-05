using System.Collections;
using System.Collections.Generic;
using Alice;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InputFocusTests {
    [UnityTest]
    public IEnumerator NestedModalRestrictsEveryPlayerAndRestoresParentAfterInputBoundary() {
        var parent = new GameObject("Parent modal");
        var child = new GameObject("Child modal");
        child.transform.SetParent(parent.transform);
        using var focus = new InputFocusService();
        using var parentScope = focus.RegisterModal(parent.transform, playerId: 0);
        try {
            yield return null;
            Assert.That(focus.CanReceive(parent.transform, 0), Is.True);
            Assert.That(focus.CanReceive(parent.transform, 1), Is.False);
            Assert.That(focus.CanReceive(null), Is.False);
            using (focus.RegisterModal(child.transform, playerId: 1)) {
                Assert.That(focus.CanReceive(child.transform, 1), Is.False, "An opening input cannot also operate the new modal.");
                yield return null;
                Assert.That(focus.CanReceive(child.transform, 1), Is.True);
                Assert.That(focus.CanPoint(parent.transform, 0), Is.False);
                Assert.That(focus.CanPoint(child.transform, 0), Is.False);
            }
            Assert.That(focus.CanReceive(parent.transform, 0), Is.False, "A closing input cannot fall through to the parent.");
            yield return null;
            Assert.That(focus.CanReceive(parent.transform, 0), Is.True);
        } finally {
            Object.DestroyImmediate(parent);
        }
    }

    [UnityTest]
    public IEnumerator ConsumedCursorClickAndScopeChangeCannotReachScreenSubscribers() {
        using var focus = new InputFocusService();
        using var registry = new GamePadRegistry(focus);
        using var device = new TestPad();
        var player = registry.RequestRegister(device);
        int screen = 0, shortcut = 0, clicks = 0;
        using var screenSubscription = player.OnButtonDown.Subscribe(_ => screen++);
        using var shortcutSubscription = registry.OnAnyButtonDown.Subscribe(_ => shortcut++);
        using (focus.RegisterCursor(0, () => { clicks++; return true; })) {
            device.Down(GamePadButton.East);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(shortcut, Is.Zero);
            Assert.That(screen, Is.Zero);
            device.Up(GamePadButton.East);
        }
        var modal = new GameObject("Modal");
        try {
            using var modalScope = focus.RegisterModal(modal.transform);
            yield return null;
            device.Down(GamePadButton.South);
            Assert.That(shortcut, Is.EqualTo(1));
            Assert.That(screen, Is.Zero);
            modal.SetActive(false);
            focus.Tick();
            yield return null;
            device.Down(GamePadButton.South);
            Assert.That(screen, Is.Zero, "A button held across closing must be released before it can operate the screen.");
            device.Up(GamePadButton.South);
            device.Down(GamePadButton.South);
            Assert.That(screen, Is.EqualTo(1));
        } finally {
            Object.DestroyImmediate(modal);
        }
    }

    [Test]
    public void OutstandingScopesCanBeReleasedAfterApplicationDisposal() {
        var focus = new InputFocusService();
        var block = focus.Block();
        var hold = focus.HoldScreen();
        focus.Dispose();
        Assert.DoesNotThrow(block.Dispose);
        Assert.DoesNotThrow(hold.Dispose);
        Assert.DoesNotThrow(focus.Dispose);
        Assert.That(focus.HasTextFocus, Is.False);
    }

    [Test]
    public void DirectionCancellationStopsCursorEvenWithoutAZeroDirectionEvent() {
        using var focus = new InputFocusService();
        using var registry = new GamePadRegistry(focus);
        using var device = new TestPad();
        var player = registry.RequestRegister(device);
        var direction = Vector2.zero;
        using var subscription = player.OnCursorDirection.Subscribe(value => direction = value);
        device.Direction(Vector2.right);
        Assert.That(direction, Is.EqualTo(Vector2.right));
        device.CancelDirection();
        Assert.That(direction, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void VirtualTouchCancellationBalancesMultiplePointersAndAllowsFreshPresses() {
        var owner = new GameObject("Inactive virtual touch fixture");
        owner.SetActive(false);
        var view = owner.AddComponent<VirtualTouchControllerCanvasView>();
        int down = 0, up = 0;
        using var downSubscription = view.OnButtonDown.Subscribe(_ => down++);
        using var upSubscription = view.OnButtonUp.Subscribe(_ => up++);
        try {
            view.EmitButtonDown(GamePadButton.East, 10);
            view.EmitButtonDown(GamePadButton.East, 20);
            Assert.That(down, Is.EqualTo(1));
            view.EmitButtonUp(GamePadButton.East, 10);
            Assert.That(up, Is.Zero, "Another pointer is still holding this button.");
            view.CancelPointerInput();
            Assert.That(up, Is.EqualTo(1));
            view.EmitButtonUp(GamePadButton.East, 20);
            Assert.That(up, Is.EqualTo(1), "A late release after reset must not release a button twice.");
            view.EmitButtonDown(GamePadButton.East, 10);
            view.EmitButtonUp(GamePadButton.East, 10);
            Assert.That(down, Is.EqualTo(2));
            Assert.That(up, Is.EqualTo(2));
        } finally {
            Object.DestroyImmediate(owner);
        }
    }

    [UnityTest]
    public IEnumerator NativeRaycasterRequiresBindingAndFiltersBackgroundGraphics() {
        var owner = new GameObject("Native raycast fixture", typeof(RectTransform), typeof(Canvas), typeof(FocusGraphicRaycaster));
        var events = new GameObject("Raycast events", typeof(EventSystem));
        var modal = new GameObject("Other modal");
        using var focus = new InputFocusService();
        try {
            owner.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var graphic = new GameObject("Background control", typeof(RectTransform), typeof(Image));
            graphic.transform.SetParent(owner.transform, false);
            var rect = graphic.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { position = new Vector2(Screen.width, Screen.height) * 0.5f };
            var raycaster = owner.GetComponent<FocusGraphicRaycaster>();
            var hits = new List<RaycastResult>();
            raycaster.Raycast(pointer, hits);
            Assert.That(hits, Is.Empty, "Missing initialization must never expose background controls.");
            raycaster.Initialize(focus);
            raycaster.Raycast(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            hits.Clear();
            using var modalScope = focus.RegisterModal(modal.transform);
            yield return null;
            raycaster.Raycast(pointer, hits);
            Assert.That(hits, Is.Empty);
        } finally {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(events);
            Object.DestroyImmediate(modal);
        }
    }

    sealed class TestPad : IGamePad, System.IDisposable {
        readonly Subject<GamePadButton> down = new();
        readonly Subject<GamePadButton> up = new();
        readonly Subject<Vector2> direction = new();
        readonly Subject<Unit> directionCanceled = new();
        public string DeviceName => "Test controller";
        public Observable<Vector2> OnDirectionAsObservable => direction;
        public Observable<Unit> OnDirectionCanceledAsObservable => directionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => down;
        public Observable<GamePadButton> OnButtonUpAsObservable => up;
        public void Direction(Vector2 value) => direction.OnNext(value);
        public void CancelDirection() => directionCanceled.OnNext(Unit.Default);
        public void Down(GamePadButton button) => down.OnNext(button);
        public void Up(GamePadButton button) => up.OnNext(button);
        public void DestroyGamePad() { }
        public void Dispose() { down.Dispose(); up.Dispose(); direction.Dispose(); directionCanceled.Dispose(); }
    }
}
