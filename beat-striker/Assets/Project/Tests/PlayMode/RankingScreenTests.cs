#if UNITY_EDITOR
using System.Collections;
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
        for (var i = 0; i < 80; i++) {
            scroll.verticalNormalizedPosition = 0;
            yield return null;
        }
        Assert.That(content.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "<b>49</b>"), Is.True);
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
        using var presenter = new RankingPresenter(view, list, new ImmediateTransition(), api, null, null, null, null);
        presenter.Dispose();
        api.Histories.SetResult(new[] { new BattleHistorySummary { id = "late-record" } });
        yield return null;
        yield return null;
        Assert.That(list.GetItemCount(), Is.Zero, "A departed screen must ignore late network responses.");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator LeavingScreenIgnoresLateHistoryFailure() {
        owner = new GameObject("Inactive ranking views");
        owner.SetActive(false);
        var view = owner.AddComponent<RankingPresenterView>();
        var list = owner.AddComponent<RankingHistoryListView>();
        var api = new DelayedHistoryApi();
        ExpectHistoryRequestLogs();
        using var presenter = new RankingPresenter(view, list, new ImmediateTransition(), api, null, null, null, null);
        presenter.Dispose();
        api.Histories.SetException(new System.InvalidOperationException("Delayed network failure"));
        yield return null;
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }

    static void ExpectHistoryRequestLogs() {
        LogAssert.Expect(LogType.Log, "[RankingPresenter] Constructed and subscribing view events");
        LogAssert.Expect(LogType.Log, "[RankingPresenter] EnterRankingAsync requesting end transition. scene=Ranking");
        LogAssert.Expect(LogType.Log, "[RankingPresenter] EnterRankingAsync end transition completed. isSuccess=True");
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
