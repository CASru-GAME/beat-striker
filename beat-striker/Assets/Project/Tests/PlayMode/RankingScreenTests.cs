#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Alice;
using Core;
using NUnit.Framework;
using PolyAndCode.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class RankingScreenTests {
    const string CellPath = "Assets/Project/Screen/Ranking/RankingHistoryCellView.prefab";
    GameObject owner;

    [TearDown]
    public void TearDown() {
        if (owner) Object.DestroyImmediate(owner);
    }

    [UnityTest]
    public IEnumerator SerializedCellRebindsReplayAndResetsHover() {
        owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CellPath));
        var cell = owner.GetComponent<RankingHistoryCellView>();
        var button = owner.GetComponent<Botan>();
        var art = owner.transform.Find("Match Strip");
        string replayId = null;
        cell.ConfigureCell(Entry("first", true), id => replayId = id);
        button.OnPointerEnter(null);
        Assert.That(art.localScale.x, Is.GreaterThan(1));
        button.OnPointerDown(null);
        Assert.That(replayId, Is.EqualTo("first"));

        cell.ConfigureCell(Entry("second", false), id => replayId = id);
        Assert.That(art.localScale, Is.EqualTo(Vector3.one));
        Assert.That(art.Find("Replay Plate").gameObject.activeSelf, Is.False);
        button.OnPointerDown(null);
        Assert.That(replayId, Is.EqualTo("first"), "A record without replay must not emit a request.");

        cell.ConfigureCell(Entry("third", true), id => replayId = id);
        button.OnPointerDown(null);
        Assert.That(replayId, Is.EqualTo("third"), "A recycled cell must use the current record.");
        Assert.That(art.Find("Player One Name").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("<b>third</b>"));
        Assert.That(art.Find("Player One Name").GetComponent<TextMeshProUGUI>().richText, Is.False);
        Assert.That(owner.GetComponentsInChildren<RankingSlantedGraphic>(true).Count(g => g.name == "White Edge"), Is.EqualTo(1));
        yield return null;
    }

    [UnityTest]
    public IEnumerator HistoryReloadHandlesEmptyRapidChangesAndRecycledRows() {
        owner = new GameObject("Ranking test", typeof(RectTransform), typeof(Canvas));
        owner.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var viewport = Rect("Viewport", owner.transform, new Vector2(604, 360));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport, new Vector2(604, 0));
        var scroll = viewport.gameObject.AddComponent<RecyclableScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.PrototypeCell = AssetDatabase.LoadAssetAtPath<GameObject>(CellPath).GetComponent<RectTransform>();
        var listObject = new GameObject("List");
        listObject.SetActive(false);
        listObject.transform.SetParent(owner.transform);
        var list = listObject.AddComponent<RankingHistoryListView>();
        typeof(RankingHistoryListView).GetField("scrollRect", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(list, scroll);
        listObject.SetActive(true);
        var status = Rect("Status", owner.transform, new Vector2(604, 100)).gameObject.AddComponent<TextMeshProUGUI>();
        list.SetStatusLabel(status);
        Assert.That(status.text, Does.Contain("読み込み中"));
        yield return null;
        yield return null;

        list.SetEntries(Enumerable.Range(0, 50).Select(i => Entry(i.ToString(), i % 2 == 0)));
        yield return null;
        yield return null;
        Assert.That(status.gameObject.activeSelf, Is.False);
        Assert.That(content.childCount, Is.GreaterThan(2));

        var viewObject = new GameObject("Inactive ranking view");
        viewObject.SetActive(false);
        viewObject.transform.SetParent(owner.transform);
        var view = viewObject.AddComponent<RankingPresenterView>();
        typeof(RankingPresenterView).GetField("battleHistoryScrollRect", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, scroll);
        typeof(RankingPresenterView).GetField("historyDialog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, owner);
        typeof(RankingPresenterView).GetField("gradeScroll", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, scroll);
        var updateView = typeof(RankingPresenterView).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        var cursorObject = new GameObject("Game cursor", typeof(RectTransform));
        cursorObject.transform.SetParent(owner.transform, false);
        cursorObject.AddComponent<Alice.Cursor>();
        scroll.verticalNormalizedPosition = 0.5f;
        cursorObject.transform.position = viewport.TransformPoint(new Vector3(0, viewport.rect.yMin - 30, 0));
        yield return null;
        var before = scroll.verticalNormalizedPosition;
        updateView.Invoke(view, null);
        Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(before), "Holding the cursor past the bottom edge must scroll down.");
        cursorObject.transform.position = viewport.TransformPoint(new Vector3(0, viewport.rect.yMax + 30, 0));
        before = scroll.verticalNormalizedPosition;
        updateView.Invoke(view, null);
        Assert.That(scroll.verticalNormalizedPosition, Is.GreaterThan(before), "Holding the cursor past the top edge must scroll up.");
        cursorObject.transform.position = viewport.TransformPoint(new Vector3(0, viewport.rect.center.y, 0));
        before = scroll.verticalNormalizedPosition;
        updateView.Invoke(view, null);
        Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(before), "Leaving the edge must stop scrolling.");
        cursorObject.SetActive(false);

        for (var i = 0; i < 80; i++) {
            scroll.verticalNormalizedPosition = 0;
            yield return null;
        }
        Assert.That(content.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "<b>49</b>"), Is.True);

        list.SetEntries(Enumerable.Range(0, 50).Select(i => Entry(i.ToString(), true)), 37.25f);
        yield return null;
        yield return null;
        Assert.That(list.RowPosition, Is.EqualTo(37.25f).Within(0.01f), "Restoring a recycled list must use the logical row and its fractional offset.");
        Assert.That(content.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "<b>37</b>"), Is.True);
        var savedPosition = list.RowPosition;
        list.SetEntries(Enumerable.Range(0, 75).Select(i => Entry(i.ToString(), true)), savedPosition);
        list.SetEntries(Enumerable.Range(0, 80).Select(i => Entry(i.ToString(), true)), savedPosition);
        yield return null;
        yield return null;
        Assert.That(list.RowPosition, Is.EqualTo(savedPosition).Within(0.01f), "Appending and rapid reloads must retain the current logical row.");
        Assert.That(content.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "<b>37</b>"), Is.True);
        viewport.gameObject.SetActive(false);
        list.SetEntries(Enumerable.Range(0, 80).Select(i => Entry(i.ToString(), true)), savedPosition);
        yield return null;
        viewport.gameObject.SetActive(true);
        yield return null;
        yield return null;
        Assert.That(list.RowPosition, Is.EqualTo(savedPosition).Within(0.01f), "Loading a closed modal must defer its coroutine until the scroll becomes active again.");
        for (var i = 0; i < 80; i++) {
            scroll.verticalNormalizedPosition = 1;
            yield return null;
        }
        Assert.That(content.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "<b>0</b>"), Is.True);

        list.SetEntries(null);
        list.SetEntries(new[] { Entry("temporary", false) });
        list.SetEntries(null);
        yield return null;
        yield return null;
        yield return null;
        Assert.That(content.childCount, Is.Zero);
        Assert.That(status.text, Does.Contain("まだ対戦履歴がありません"));
        list.SetEntries(new[] { Entry("restored", true) });
        yield return null;
        yield return null;
        yield return null;
        Assert.That(content.childCount, Is.EqualTo(1));
        Assert.That(status.gameObject.activeSelf, Is.False);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ScenePreviewIsDisabledBeforeRuntimeRowsAppear() {
        owner = new GameObject("Editor Preview Matches", typeof(RectTransform));
        owner.SetActive(false);
        owner.AddComponent<RankingEditorPreview>();
        owner.SetActive(true);
        yield return null;
        Assert.That(owner.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator LeavingScreenIgnoresLateHistoryResponse() {
        owner = new GameObject("Inactive ranking views");
        owner.SetActive(false);
        var view = owner.AddComponent<RankingPresenterView>();
        var list = owner.AddComponent<RankingHistoryListView>();
        var api = new DelayedHistoryApi();
        ExpectHistoryRequestLogs();
        using var presenter = new RankingPresenter(view, list, new ImmediateTransition(), api, null, null, null, null, null, null, null, null, new PlayerSession(), new RankingNavigationState());
        presenter.Dispose();
        api.Histories.SetResult(new[] { new BattleHistorySummary { id = "late-record" } });
        yield return null;
        yield return null;
        Assert.That(list.GetItemCount(), Is.Zero, "A departed screen must ignore late network responses.");
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void InvalidReplayIsRejectedBeforeTransition() {
        owner = new GameObject("Inactive ranking views");
        owner.SetActive(false);
        var view = owner.AddComponent<RankingPresenterView>();
        var list = owner.AddComponent<RankingHistoryListView>();
        var api = new DelayedHistoryApi();
        ExpectHistoryRequestLogs();
        using var presenter = new RankingPresenter(view, list, new ImmediateTransition(), api, null, null, null, null, new TestMusicRegistry(), new TestStrikerRegistry(), null, null, new PlayerSession(), new RankingNavigationState());
        var check = typeof(RankingPresenter).GetMethod("CanPlayReplay", BindingFlags.Instance | BindingFlags.NonPublic);
        var replay = new ReplayPayload { stage = "Street", musicId = "known", strikerIds = new[] { 0, 1 }, rounds = new[] { new ReplayRoundPayload() } };
        Assert.That(check.Invoke(presenter, new object[] { replay }), Is.True);
        replay.musicId = "mock-display-only";
        Assert.That(check.Invoke(presenter, new object[] { replay }), Is.False);
        replay.musicId = "known";
        replay.rounds = System.Array.Empty<ReplayRoundPayload>();
        Assert.That(check.Invoke(presenter, new object[] { replay }), Is.False);
        replay.rounds = new[] { new ReplayRoundPayload() };
        replay.strikerIds = new[] { 0, 999 };
        Assert.That(check.Invoke(presenter, new object[] { replay }), Is.False);
    }

    [UnityTest]
    public IEnumerator ReplayReturnRetainsLoadedRankingPagesAndNextCursor() {
        var navigation = new RankingNavigationState { ListPosition = 0.37f };
        navigation.CaptureRanking(Enumerable.Range(0, 60).Select(i => new PlayerProfile { name = $"player-{i}", grade = 0 }), new[] { "page-3", "", "", "", "", "" });
        var records = new RecordsApi {
            RankingPage = (grade, cursor) => new PlayerProfilePage { items = new[] { new PlayerProfile { name = "player-60", grade = grade } } },
        };
        using var session = new PlayerSession();
        ExpectEnterRankingLogs();
        using var presenter = CreatePresenter(records, navigation, session, out var view, out _, out _);
        yield return null;
        yield return null;
        Assert.That(records.RankingRequests, Is.Empty, "Returning from a replay must retain pages already loaded instead of resetting them to page one.");
        Assert.That(RankingRows(presenter).Count, Is.EqualTo(60));
        Assert.That(RankingRows(presenter).Any(profile => profile.name == "player-59"), Is.True);
        Assert.That(view.ListPosition, Is.EqualTo(0.37f).Within(0.01f));

        yield return Await(InvokeTask(presenter, "LoadGradeAsync", 0, false));
        Assert.That(records.RankingRequests.Single().Cursor, Is.EqualTo("page-3"));
        Assert.That(RankingRows(presenter).Count, Is.EqualTo(61));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ReplayReturnCompletesOnlyPreviouslyUnfinishedGrades() {
        var navigation = new RankingNavigationState();
        navigation.CaptureRanking(new[] { new PlayerProfile { name = "later-page-player", grade = 0 } }, new[] { "page-3", "", null, "", "", "" });
        var records = new RecordsApi();
        using var session = new PlayerSession();
        ExpectEnterRankingLogs();
        using var presenter = CreatePresenter(records, navigation, session, out _, out _, out _);
        yield return null;
        yield return null;
        Assert.That(records.RankingRequests.Count, Is.EqualTo(1));
        Assert.That(records.RankingRequests[0].Grade, Is.EqualTo(2));
        Assert.That(records.RankingRequests[0].Cursor, Is.Empty);
        Assert.That(RankingRows(presenter).Single().name, Is.EqualTo("later-page-player"));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator OlderGradeResponseCannotOverwriteACompletedPromotion() {
        var lower = new TaskCompletionSource<PlayerProfilePage>();
        var higher = new TaskCompletionSource<PlayerProfilePage>();
        var records = new RecordsApi();
        records.PendingRankings.Add(1, lower.Task);
        records.PendingRankings.Add(2, higher.Task);
        using var session = new PlayerSession();
        ExpectEnterRankingLogs();
        using var presenter = CreatePresenter(records, new RankingNavigationState(), session, out _, out _, out _);
        yield return null;
        yield return null;
        higher.SetResult(new PlayerProfilePage { items = new[] { new PlayerProfile { name = "promoted-player", grade = 2 } } });
        yield return null;
        lower.SetResult(new PlayerProfilePage { items = new[] { new PlayerProfile { name = "promoted-player", grade = 1 } } });
        yield return null;
        yield return null;
        Assert.That(RankingRows(presenter).Single().grade, Is.EqualTo(2), "The completion order of old and new grade requests must never demote a player.");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator FailedInitialGradePageCanBeRetriedWithoutLeavingRanking() {
        var records = new RecordsApi();
        records.PendingRankings.Add(2, Task.FromException<PlayerProfilePage>(new System.InvalidOperationException("Expected first page failure")));
        using var session = new PlayerSession();
        ExpectEnterRankingLogs();
        LogAssert.Expect(LogType.Log, "[Ranking] Expected first page failure");
        using var presenter = CreatePresenter(records, new RankingNavigationState(), session, out _, out _, out _);
        yield return null;
        yield return null;
        var more = (bool[])typeof(RankingPresenter).GetField("hasMore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
        Assert.That(more[2], Is.True, "A failed first page must expose the grade's retry button, even when no rows have loaded.");
        records.PendingRankings.Remove(2);
        yield return Await(InvokeTask(presenter, "LoadGradeAsync", 2, false));
        Assert.That(records.RankingRequests.Last().Grade, Is.EqualTo(2));
        Assert.That(records.RankingRequests.Last().Cursor, Is.Empty);
        Assert.That(more[2], Is.False);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator FailedHistoryNextPageRetainsRowsAndCanRetryTheSameCursor() {
        var navigation = new RankingNavigationState();
        navigation.CaptureRanking(System.Array.Empty<PlayerProfile>(), new[] { "", "", "", "", "", "" });
        var records = new RecordsApi();
        records.Histories.Enqueue(Task.FromResult(new BattleHistoryListResponse { items = new[] { new BattleHistorySummary { id = "first" } }, nextCursor = "history-page-2" }));
        records.Histories.Enqueue(Task.FromException<BattleHistoryListResponse>(new System.InvalidOperationException("Expected history failure")));
        records.Histories.Enqueue(Task.FromResult(new BattleHistoryListResponse { items = new[] { new BattleHistorySummary { id = "second" } } }));
        using var session = new PlayerSession();
        ExpectEnterRankingLogs();
        using var presenter = CreatePresenter(records, navigation, session, out _, out var list, out var more);
        yield return null;
        yield return null;
        yield return Await(InvokeTask(presenter, "LoadPlayerHistoryAsync", "player", false));
        Assert.That(list.GetItemCount(), Is.EqualTo(1));
        Assert.That(more.gameObject.activeSelf, Is.True);

        LogAssert.Expect(LogType.Log, "[Ranking] Expected history failure");
        yield return Await(InvokeTask(presenter, "LoadPlayerHistoryAsync", "player", true));
        Assert.That(list.GetItemCount(), Is.EqualTo(1));
        Assert.That(more.gameObject.activeSelf, Is.True, "A failed additional page must leave its retry control available.");
        yield return Await(InvokeTask(presenter, "LoadPlayerHistoryAsync", "player", true));
        Assert.That(records.HistoryCursors, Is.EqualTo(new[] { "", "history-page-2", "history-page-2" }));
        Assert.That(list.GetItemCount(), Is.EqualTo(2));
        Assert.That(more.gameObject.activeSelf, Is.False);
        LogAssert.NoUnexpectedReceived();
    }

    static List<PlayerProfile> RankingRows(RankingPresenter presenter) => (List<PlayerProfile>)typeof(RankingPresenter).GetField("ranking", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
    static Task InvokeTask(RankingPresenter presenter, string method, params object[] arguments) => (Task)typeof(RankingPresenter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presenter, arguments);
    static IEnumerator Await(Task task) {
        while (!task.IsCompleted) yield return null;
        task.GetAwaiter().GetResult();
    }

    RankingPresenter CreatePresenter(RecordsApi records, RankingNavigationState navigation, PlayerSession session, out RankingPresenterView view, out RankingHistoryListView list, out Botan more) {
        owner = new GameObject("Inactive ranking fixture", typeof(RectTransform));
        owner.SetActive(false);
        view = owner.AddComponent<RankingPresenterView>();
        list = owner.AddComponent<RankingHistoryListView>();
        var viewport = Rect("History viewport", owner.transform, new Vector2(604, 360));
        var historyScroll = viewport.gameObject.AddComponent<RecyclableScrollRect>();
        historyScroll.viewport = viewport;
        historyScroll.content = Rect("History content", viewport, new Vector2(604, 2000));
        SetField(list, "scrollRect", historyScroll);
        var gradeViewport = Rect("Grade viewport", owner.transform, new Vector2(604, 360));
        var gradeScroll = gradeViewport.gameObject.AddComponent<ScrollRect>();
        gradeScroll.viewport = gradeViewport;
        gradeScroll.content = Rect("Grade content", gradeViewport, new Vector2(604, 2000));
        SetField(view, "gradeScroll", gradeScroll);
        SetField(view, "gradeSections", System.Array.Empty<RankingGradeSectionView>());
        SetField(view, "rankingStatus", Rect("Status", owner.transform, new Vector2(604, 100)).gameObject.AddComponent<TextMeshProUGUI>());
        SetField(view, "historyHeading", Rect("Heading", owner.transform, new Vector2(604, 100)).gameObject.AddComponent<TextMeshProUGUI>());
        var dialog = new GameObject("History dialog");
        dialog.transform.SetParent(owner.transform);
        SetField(view, "historyDialog", dialog);
        var moreObject = new GameObject("More history");
        moreObject.transform.SetParent(owner.transform);
        more = moreObject.AddComponent<Botan>();
        SetField(view, "moreHistoryEmitter", more);
        return new RankingPresenter(view, list, new ImmediateTransition(), new DelayedHistoryApi(), null, null, null, null, new TestMusicRegistry(), new TestStrikerRegistry(), records, new Profiles(), session, navigation);
    }

    static void SetField(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    static void ExpectEnterRankingLogs() {
        ExpectHistoryRequestLogs();
        LogAssert.Expect(LogType.Log, "[RankingPresenter] EnterRankingAsync requesting end transition. scene=Ranking");
        LogAssert.Expect(LogType.Log, "[RankingPresenter] EnterRankingAsync end transition completed. isSuccess=True");
    }

    class RecordsApi : IPlayerRecordsApiClient {
        public readonly List<(int Grade, string Cursor)> RankingRequests = new();
        public readonly List<string> HistoryCursors = new();
        public readonly Queue<Task<BattleHistoryListResponse>> Histories = new();
        public readonly Dictionary<int, Task<PlayerProfilePage>> PendingRankings = new();
        public System.Func<int, string, PlayerProfilePage> RankingPage = (grade, cursor) => new PlayerProfilePage();
        public Task<PlayerProfile> GetProfileAsync(PlayerProfile profile) => throw new System.NotSupportedException();
        public Task<PlayerProfilePage> GetRankingAsync(int grade, string cursor) {
            RankingRequests.Add((grade, cursor));
            return PendingRankings.TryGetValue(grade, out var pending) ? pending : Task.FromResult(RankingPage(grade, cursor));
        }
        public Task<BattleHistoryListResponse> GetHistoryAsync(string name, string cursor) {
            HistoryCursors.Add(cursor);
            return Histories.Dequeue();
        }
    }

    class Profiles : IPlayerProfileService {
        PlayerProfile[] ranking = System.Array.Empty<PlayerProfile>();
        public IReadOnlyList<PlayerProfile> CachedRanking => ranking;
        public R3.Observable<R3.Unit> Changed => R3.Observable.Empty<R3.Unit>();
        public int CpuLevel { get; set; }
        public void CacheRanking(IEnumerable<PlayerProfile> profiles) => ranking = profiles.ToArray();
        public PlayerProfile Get(string name) => throw new System.NotSupportedException();
        public Task<PlayerProfile> SelectNameAsync(int slot, string name) => throw new System.NotSupportedException();
        public void RememberName(int slot, string name) => throw new System.NotSupportedException();
        public void Record(BattleHistorySaveRequest request) => throw new System.NotSupportedException();
        public Task SynchronizeAsync() => throw new System.NotSupportedException();
    }

    class TestMusicRegistry : IMusicRegistry {
        public MusicInfo Default => GetAll()[0];
        public MusicInfo GetById(string id) => throw new System.NotSupportedException();
        public System.Collections.Generic.IReadOnlyList<MusicInfo> GetAll() => new[] { new MusicInfo("known", "", "", "", 0, 0) };
        public Awaitable<LoadedAsset<AudioClip>> LoadAudioClipAsync(string id) => throw new System.NotSupportedException();
        public Awaitable<LoadedAsset<AudioClip>> LoadPreviewAudioClipAsync(string id) => throw new System.NotSupportedException();
        public Awaitable<LoadedAsset<TextAsset>> LoadSpectrumDataAsync(string id) => throw new System.NotSupportedException();
        public Awaitable<LoadedAsset<TextAsset>> LoadBeatDataAsync(string id) => throw new System.NotSupportedException();
    }

    class TestStrikerRegistry : IAppStrikerRegistry {
        public StrikerInfo Default => GetAll()[0];
        public StrikerInfo GetByStriker(Striker striker) => throw new System.NotSupportedException();
        public System.Collections.Generic.IReadOnlyList<StrikerInfo> GetAll() => new[] { new StrikerInfo("", Striker.Hero, null, null), new StrikerInfo("", Striker.Wizard, null, null) };
        public Awaitable<LoadedAsset<GameObject>> LoadBattlePrefabAsync(Striker striker) => throw new System.NotSupportedException();
    }

    [UnityTest]
    public IEnumerator LeavingScreenIgnoresLateHistoryFailure() {
        owner = new GameObject("Inactive ranking views");
        owner.SetActive(false);
        var view = owner.AddComponent<RankingPresenterView>();
        var list = owner.AddComponent<RankingHistoryListView>();
        var api = new DelayedHistoryApi();
        ExpectHistoryRequestLogs();
        using var presenter = new RankingPresenter(view, list, new ImmediateTransition(), api, null, null, null, null, null, null, null, null, new PlayerSession(), new RankingNavigationState());
        presenter.Dispose();
        api.Histories.SetException(new System.InvalidOperationException("Delayed network failure"));
        yield return null;
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }

    static void ExpectHistoryRequestLogs() {
        LogAssert.Expect(LogType.Log, "[RankingPresenter] Constructed and subscribing view events");
    }

    class DelayedHistoryApi : IBattleHistoryApiClient {
        public readonly TaskCompletionSource<BattleHistorySummary[]> Histories = new();
        public Task<BattleHistorySummary[]> GetSummariesAsync(int limit) => Histories.Task;
        public Task<BattleHistoryCreateResponse> SaveAsync(BattleHistorySaveRequest request) => throw new System.NotSupportedException();
        public Task<BattleHistoryDetail> GetDetailAsync(string id) => throw new System.NotSupportedException();
    }

    class ImmediateTransition : ISceneTransitionService {
        public bool IsTransitioning => false;
        public R3.Observable<bool> TransitioningChanged => R3.Observable.Empty<bool>();
        public R3.Observable<AppScene> EndTransitionCompleted => R3.Observable.Empty<AppScene>();
        public ISceneTransitionService.StartResult RequestStartTransition(AppScene scene) => new(false);
        public Task<ISceneTransitionService.ExitResult> RequestEndTransitionAsync(AppScene scene) => Task.FromResult(new ISceneTransitionService.ExitResult(true));
    }

    static RankingBattleHistoryEntry Entry(string id, bool replay) => new(
        id, $"<b>{id}</b>", "ストライカー", "2026/11/03 20:32", "1P WIN!", "Street / Beat the Night", replay);

    static RectTransform Rect(string name, Transform parent, Vector2 size) {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = size;
        return rect;
    }
}
#endif
