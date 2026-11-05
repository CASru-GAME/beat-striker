#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Alice;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ChoiceDialogPresenterTests {
    const string DialogPath = "Assets/Project/App/UI/ChoiceDialog.prefab";
    GameObject owner;

    [TearDown]
    public void TearDown() {
        if (owner) Object.DestroyImmediate(owner);
    }

    [UnityTest]
    public IEnumerator DestroyingViewCancelsPendingDialogAndReleasesAppInputSubscriptions() {
        owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath));
        var view = owner.GetComponentInChildren<ChoiceDialogView>(true);
        using var session = new PlayerSession();
        using var pads = new CountingPads();
        using var presenter = new ChoiceDialogPresenter(view, session, pads);
        var pending = presenter.AskAsync("名前", "入力", new[] { "OK" }, input: "プレイヤー");
        Assert.That(session.IsNameInputActive, Is.True);
        Assert.That(pads.ActiveSubscriptions, Is.EqualTo(9));

        Object.Destroy(owner);
        yield return null;
        Assert.That(pending.IsCanceled, Is.True);
        Assert.That(session.IsNameInputActive, Is.False);
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
        using var session = new PlayerSession();
        using var pads = new CountingPads();
        using var presenter = new ChoiceDialogPresenter(view, session, pads);
        var heading = typeof(ChoiceDialogView).GetField("heading", BindingFlags.Instance | BindingFlags.NonPublic);
        var originalHeading = heading.GetValue(view);
        heading.SetValue(view, null);
        try {
            Assert.Throws<NullReferenceException>(() => presenter.AskAsync("名前", "入力", new[] { "OK" }, input: "プレイヤー"));
            Assert.That(session.IsNameInputActive, Is.False, "A failure while showing the dialog must restore normal keyboard input.");
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

    class CountingPads : IGamePadRegistry, IDisposable {
        readonly Pad[] pads;
        public int ActiveSubscriptions { get; private set; }
        public CountingPads() => pads = new[] { new Pad(0, this), new Pad(1, this), new Pad(2, this), new Pad(3, this) };
        public Observable<PlayerGamePadButtonEvent> OnAnyButtonDown => Track<PlayerGamePadButtonEvent>();
        public IPlayerGamePad Get(int playerId) => pads[playerId];
        Observable<T> Track<T>() => Observable.Create<T>(_ => {
            ActiveSubscriptions++;
            return Disposable.Create(() => ActiveSubscriptions--);
        });
        public void Dispose() { foreach (var pad in pads) pad.Dispose(); }
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
        public void RotateFaceButtonWiringClockwise() => throw new NotSupportedException();
        public GamePadButton GetPhysicalFaceButton(GamePadButton button) => button;

        class Pad : IPlayerGamePad, IDisposable {
            readonly CountingPads owner;
            readonly ReactiveProperty<bool> hasGamePad = new(false);
            public int PlayerId { get; }
            public string DeviceName => "Test controller";
            public ReadOnlyReactiveProperty<bool> HasGamePad => hasGamePad;
            public Observable<Vector2> OnDirection => owner.Track<Vector2>();
            public Observable<Unit> OnDirectionCanceled => owner.Track<Unit>();
            public Observable<GamePadButton> OnButtonDown => Observable.Empty<GamePadButton>();
            public Observable<GamePadButton> OnButtonUp => Observable.Empty<GamePadButton>();
            public Pad(int playerId, CountingPads owner) { PlayerId = playerId; this.owner = owner; }
            public void Dispose() => hasGamePad.Dispose();
        }
    }
}
#endif
