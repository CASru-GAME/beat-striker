#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Alice;
using Core;
using Fusion;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class OnlineRoomTests {
    const string CanvasPath = "Assets/Project/Screen/OnlineRoom/OnlineRoomCanvas.prefab";
    GameObject owner;
    OnlineRoomPresenter presenter;

    [TearDown]
    public void TearDown() {
        presenter?.Dispose();
        presenter = null;
        if (owner) Object.DestroyImmediate(owner);
    }

    [UnityTest]
    public IEnumerator SelectingExactPlayerPreventsDuplicateAndUnavailableChallenges() {
        var client = CreateRoom(out var view);
        client.roster.Value = new(true, new[] { Player("first"), Player("target"), Player("busy", false) });
        yield return null;
        ClickRow("PLAYER_BUSY");
        Click("Declare War");
        Assert.That(client.invited, Is.Null);
        ClickRow("PLAYER_TARGET");
        Click("Declare War");
        Click("Declare War");
        Assert.That(client.invited, Is.EqualTo("target"));
        Assert.That(client.inviteCount, Is.EqualTo(1));
        client.state.Value = client.state.Value with { UiMode = OnlineDuelUiMode.InviteSent, InviteId = "invite-1", OpponentSessionId = "target" };
        Assert.That(Find("Cancel Challenge").activeSelf, Is.True);
        Click("Cancel Challenge");
        Assert.That(client.cancelCount, Is.EqualTo(1));
        client.state.Value = OnlineDuelUiState.Idle("local");
        client.roster.Value = new(true, new[] { Player("first") });
        yield return null;
        Click("Declare War");
        Assert.That(client.inviteCount, Is.EqualTo(1), "A departed selection must not remain actionable.");
        Assert.That(Find("Selected Player").GetComponent<TextMeshProUGUI>().text, Does.Contain("選べ"));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator IncomingActionsAndBackCancelTheCurrentInvite() {
        var client = CreateRoom(out var view);
        client.state.Value = client.state.Value with { UiMode = OnlineDuelUiMode.IncomingInvite, InviteId = "incoming", InviteFromSessionId = "challenger" };
        Assert.That(Find("Incoming Challenge Actions").activeSelf, Is.True);
        Click("Accept Challenge");
        Click("Accept Challenge");
        Assert.That(client.acceptCount, Is.EqualTo(1));
        client.state.Value = client.state.Value with { Message = "new event" };
        Click("Reject Challenge");
        Assert.That(client.rejectCount, Is.EqualTo(1));
        client.state.Value = client.state.Value with { Message = "pending" };
        Click("Back To Title");
        Assert.That(client.rejectCount, Is.EqualTo(2));
        Assert.That(client.transitions.LastScene, Is.EqualTo(AppScene.Title));
        presenter.Dispose();
        client.roster.Value = new(true, new[] { Player("late") });
        Assert.That(owner.GetComponentsInChildren<OnlineRoomPlayerView>().Length, Is.Zero, "Disposed screens must ignore late roster updates.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator FullRosterScrollsAndDisconnectRemovesStaleRows() {
        var client = CreateRoom(out var view);
        client.roster.Value = new(true, Enumerable.Range(0, 31).Select(i => Player($"peer{i:00}")).ToArray());
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        var scroll = owner.GetComponentInChildren<ScrollRect>();
        Assert.That(scroll.content.childCount, Is.EqualTo(31));
        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
        Assert.That(scroll.content.pivot.y, Is.EqualTo(1));
        scroll.verticalNormalizedPosition = 0;
        Canvas.ForceUpdateCanvases();
        var last = scroll.content.GetChild(30).GetComponent<RectTransform>();
        Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, RectTransformUtility.WorldToScreenPoint(null, last.position)), Is.True, "The last player must be visible at the bottom of the scroll range.");
        Assert.That(Find("Online Count").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("32 ONLINE"));
        client.roster.Value = OnlineRoomRoster.Disconnected;
        yield return null;
        Assert.That(scroll.content.childCount, Is.Zero);
        Assert.That(Find("Online Count").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("-- ONLINE"));
        client.roster.Value = new(true, Array.Empty<OnlineRoomPlayer>());
        Assert.That(Find("Empty List Message").GetComponent<TextMeshProUGUI>().text, Does.Contain("待っています"));
    }

    [Test]
    public void ServerRosterTracksJoinInviteAcceptLeaveAndExpiration() {
        owner = new GameObject("Room relay test");
        var server = owner.AddComponent<OnlineSessionRelayServer>();
        Register(server, 1, "viewer", "OnlineRoom");
        Register(server, 2, "alpha", "OnlineRoom");
        Register(server, 3, "beta", "Menu");
        Register(server, 4, "unavailable", "Live", false);
        Assert.That(Roster(server).Select(p => p.sessionId), Is.EqualTo(new[] { "alpha", "beta", "unavailable" }));
        Assert.That(Roster(server).Single(p => p.sessionId == "unavailable").canInvite, Is.False);
        var affected = new HashSet<string>();
        Assert.That(Invoke(server, "TryCreateInvite", PlayerRef.FromIndex(1), "viewer", "beta", affected), Is.True);
        Assert.That(Roster(server).Single(p => p.sessionId == "beta").canInvite, Is.False);
        var state = Invoke(server, "ComputeView", "beta", "");
        var inviteId = (string)state.GetType().GetProperty("InviteId").GetValue(state);
        Assert.That(Invoke(server, "TryAcceptInvite", PlayerRef.FromIndex(3), "beta", inviteId, affected), Is.True);
        var viewer = Invoke(server, "ComputeView", "viewer", "");
        Assert.That(viewer.GetType().GetProperty("OpponentSessionId").GetValue(viewer), Is.EqualTo("beta"));
        Assert.That(viewer.GetType().GetProperty("UiMode").GetValue(viewer), Is.EqualTo(OnlineDuelUiMode.Matched));
        Invoke(server, "RemoveSession", "alpha", "Opponent disconnected.");
        Assert.That(Roster(server).Any(p => p.sessionId == "alpha"), Is.False);
        var presences = (IDictionary)typeof(OnlineSessionRelayServer).GetField("presenceBySession", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(server);
        var entry = presences["unavailable"];
        entry.GetType().GetField("ExpiresAt").SetValue(entry, -1f);
        Assert.That(Roster(server).Any(p => p.sessionId == "unavailable"), Is.False);
    }

    [Test]
    public void RoomRosterSurvivesProtocolRoundTripAndIsNotMatchResult() {
        var payload = new OnlineDuelEventPayload { kind = (int)OnlineDuelEventKind.RoomRoster, seq = 3, roomPlayers = new[] { Player("precise-target") } };
        var decoded = OnlineDuelProtocol.DeserializeEvent(new ArraySegment<byte>(OnlineDuelProtocol.SerializeEvent(payload)));
        Assert.That(decoded.kind, Is.Not.EqualTo((int)OnlineDuelEventKind.MatchResult));
        Assert.That(decoded.roomPlayers.Single().sessionId, Is.EqualTo("precise-target"));
        Assert.That(decoded.roomPlayers.Single().canInvite, Is.True);
        using var client = new OnlineDuelFusionClient(null, new Identity());
        Invoke(client, "ApplyDuelEvent", OnlineDuelProtocol.SerializeEvent(payload));
        Assert.That(client.Roster.CurrentValue.IsConnected, Is.True);
        Assert.That(client.State.CurrentValue.UiMode, Is.EqualTo(OnlineDuelUiMode.Idle), "A roster event must not enter a battle.");
        payload.seq = 2;
        payload.roomPlayers = Array.Empty<OnlineRoomPlayer>();
        Invoke(client, "ApplyDuelEvent", OnlineDuelProtocol.SerializeEvent(payload));
        Assert.That(client.Roster.CurrentValue.Players.Single().sessionId, Is.EqualTo("precise-target"), "Stale roster packets must be ignored.");
        Invoke(client, "ResetActiveDuelState", "Disconnected");
        Assert.That(client.Roster.CurrentValue.IsConnected, Is.False);
        payload.seq = 1;
        Invoke(client, "ApplyDuelEvent", OnlineDuelProtocol.SerializeEvent(payload));
        Assert.That(client.Roster.CurrentValue.Players, Is.Empty, "A reconnected server may start a fresh sequence.");
    }

    FakeClient CreateRoom(out OnlineRoomView view) {
        owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath));
        view = owner.GetComponent<OnlineRoomView>();
        var client = new FakeClient();
        presenter = new OnlineRoomPresenter(view, client, client, client.transitions, new InputFocusService());
        return client;
    }

    GameObject Find(string name) => owner.GetComponentsInChildren<Transform>(true).First(t => t.name == name).gameObject;
    void Click(string name) => Find(name).GetComponent<Botan>().OnPointerDown(null);
    void ClickRow(string name) => owner.GetComponentsInChildren<OnlineRoomPlayerView>().First(row => row.GetComponentsInChildren<TextMeshProUGUI>().Any(label => label.text == name)).GetComponent<Botan>().OnPointerDown(null);
    static OnlineRoomPlayer Player(string id, bool available = true) => new() { sessionId = id, scene = "OnlineRoom", canInvite = available };
    static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    static void Register(OnlineSessionRelayServer server, int index, string id, string scene, bool available = true) => Invoke(server, "RegisterSession", PlayerRef.FromIndex(index), id, scene, available, OnlineDuelPlayerStatus.StageSelecting, 1);
    static OnlineRoomPlayer[] Roster(OnlineSessionRelayServer server) => (OnlineRoomPlayer[])Invoke(server, "BuildRoomRoster", "viewer");

    class FakeClient : IOnlineRoomClient, IOnlineDuelFusionClient {
        public readonly ReactiveProperty<OnlineRoomRoster> roster = new(OnlineRoomRoster.Disconnected);
        public readonly ReactiveProperty<OnlineDuelUiState> state = new(OnlineDuelUiState.Idle("local"));
        public readonly Transition transitions = new();
        public string invited;
        public int inviteCount, acceptCount, rejectCount, cancelCount;
        public ReadOnlyReactiveProperty<OnlineRoomRoster> Roster => roster;
        public ReadOnlyReactiveProperty<OnlineDuelUiState> State => state;
        public int LastSceneSyncId => 1;
        public Task RefreshRoomAsync() => Task.CompletedTask;
        public Task<bool> InvitePlayerAsync(string id) { invited = id; inviteCount++; return Task.FromResult(true); }
        public Task NotifySceneReadyAsync(AppScene scene, bool overlay) => Task.CompletedTask;
        public Task NotifyPlayerStatusAsync(OnlineDuelPlayerStatus status) => Task.CompletedTask;
        public void InviteCandidate() { }
        public void SkipCandidate() { }
        public void AcceptInvite() => acceptCount++;
        public void RejectInvite() => rejectCount++;
        public void CancelInvite() => cancelCount++;
        public void ConsumeReservation() { }
    }

    class Transition : ISceneTransitionService {
        public AppScene LastScene;
        public bool IsTransitioning => false;
        public Observable<bool> TransitioningChanged => Observable.Empty<bool>();
        public Observable<AppScene> EndTransitionCompleted => Observable.Empty<AppScene>();
        public ISceneTransitionService.StartResult RequestStartTransition(AppScene scene) { LastScene = scene; return new(true); }
        public Task<ISceneTransitionService.ExitResult> RequestEndTransitionAsync(AppScene scene) => Task.FromResult(new ISceneTransitionService.ExitResult(true));
    }

    class Identity : IOnlineDuelIdentity {
        public string DuelSessionId => "local";
    }
}
#endif
