using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class RankingPresenter : System.IDisposable {
        const string LOG_PREFIX = "[RankingPresenter]";

        enum RankingInputState {
            Ready,
            Transitioning,
        }

        readonly RankingPresenterView view;
        readonly RankingHistoryListView historyListView;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IBattleHistoryApiClient battleHistoryApiClient;
        readonly IReplaySetting replaySetting;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IMusicRegistry musicRegistry;
        readonly IAppStrikerRegistry strikerRegistry;
        readonly CompositeDisposable subscriptions = new();
        readonly IPlayerRecordsApiClient records;
        readonly IPlayerProfileService profiles;
        readonly PlayerSession session;
        readonly RankingNavigationState navigation;
        readonly List<PlayerProfile> ranking = new();
        readonly List<RankingBattleHistoryEntry> histories = new();
        readonly string[] cursors = new string[6];
        readonly bool[] hasMore = new bool[6];
        readonly bool[] loadingGrade = new bool[6];
        readonly bool[] failedGrade = new bool[6];
        string selectedPlayer = "", historyCursor = "";
        bool historyLoading;
        int historyOperation;
        RankingInputState inputState = RankingInputState.Ready;
        bool disposed;

        [Inject]
        public RankingPresenter(
            RankingPresenterView view,
            RankingHistoryListView historyListView,
            ISceneTransitionService sceneTransitionService,
            IBattleHistoryApiClient battleHistoryApiClient,
            IReplaySetting replaySetting,
            IBattleSelectSetting battleSelectSetting,
            IPlayerSelectSetting playerSelectSetting,
            IAppNetworkSetting appNetworkSetting,
            IMusicRegistry musicRegistry,
            IAppStrikerRegistry strikerRegistry, IPlayerRecordsApiClient records, IPlayerProfileService profiles, PlayerSession session, RankingNavigationState navigation) {
            this.view = view;
            this.historyListView = historyListView;
            this.sceneTransitionService = sceneTransitionService;
            this.battleHistoryApiClient = battleHistoryApiClient;
            this.replaySetting = replaySetting;
            this.battleSelectSetting = battleSelectSetting;
            this.playerSelectSetting = playerSelectSetting;
            this.appNetworkSetting = appNetworkSetting;
            this.musicRegistry = musicRegistry;
            this.strikerRegistry = strikerRegistry;
            this.records = records; this.profiles = profiles; this.session = session; this.navigation = navigation;
            view.PlayerSelected.Subscribe(name => { _ = LoadPlayerHistoryAsync(name, false); }).AddTo(subscriptions);
            view.MoreGradeRequested.Subscribe(grade => { _ = LoadGradeAsync(grade, false); }).AddTo(subscriptions);
            view.MoreHistoryRequested.Subscribe(ignored => { _ = LoadPlayerHistoryAsync(selectedPlayer, true); }).AddTo(subscriptions);
            view.CloseHistoryRequested.Subscribe(_ => { if (inputState != RankingInputState.Ready) return; historyOperation++; historyLoading = false; selectedPlayer = ""; view.CloseHistory(); }).AddTo(subscriptions);
            Debug.Log($"{LOG_PREFIX} Constructed and subscribing view events");

            this.view.BackToMenuRequested
                .Subscribe(_ => {
                    Debug.Log($"{LOG_PREFIX} BackToMenuRequested received");
                    RequestTransitionToMenu();
                })
                .AddTo(subscriptions);

            historyListView.ReplayRequested
                .Subscribe(id => {
                    Debug.Log($"{LOG_PREFIX} ReplayRequested received. id={id}");
                    _ = RequestReplayAsync(id);
                })
                .AddTo(subscriptions);

            _ = EnterRankingAsync();
        }

        async Task EnterRankingAsync() {
            await Task.Yield();
            if (disposed) return;
            Debug.Log($"{LOG_PREFIX} EnterRankingAsync requesting end transition. scene={AppScene.Ranking}");
            var result = await sceneTransitionService.RequestEndTransitionAsync(AppScene.Ranking);
            if (disposed) return;
            Debug.Log($"{LOG_PREFIX} EnterRankingAsync end transition completed. isSuccess={result.IsSuccess}");
            if (navigation.HasRankingSnapshot) {
                ranking.AddRange(navigation.RankingEntries.Select(profile => profile with { }));
                Array.Copy(navigation.RankingCursors, cursors, cursors.Length);
                for (var grade = 0; grade < hasMore.Length; grade++) hasMore[grade] = !string.IsNullOrEmpty(cursors[grade]);
            } else {
                ranking.AddRange(profiles.CachedRanking);
            }
            PresentRanking();
            view.SetRankingStatus(cursors.Any(cursor => cursor == null) ? "階級を読み込んでいます…" : "");
            if (!string.IsNullOrEmpty(navigation.PlayerName)) {
                selectedPlayer = navigation.PlayerName; historyCursor = navigation.HistoryCursor;
                histories.AddRange(navigation.Entries); historyListView.SetEntries(histories, navigation.HistoryRowPosition);
                view.ShowHistory(selectedPlayer, !string.IsNullOrEmpty(historyCursor));
            }
            // A null cursor means the initial page has not completed. An empty cursor means that grade is exhausted.
            await Task.WhenAll(Enumerable.Range(0, cursors.Length).Where(grade => cursors[grade] == null).Select(grade => LoadGradeAsync(grade, true)));
            if (disposed) return;
            Canvas.ForceUpdateCanvases(); view.ListPosition = navigation.ListPosition;
        }

        void PresentRanking() {
            view.SetRanking(ranking.Concat(profiles.CachedRanking).GroupBy(profile => profile.name)
                .Select(group => group.OrderByDescending(profile => profile.grade).ThenBy(profile => profile.acquiredAt, StringComparer.Ordinal).First())
                .OrderBy(p => p.grade).ThenBy(p => p.acquiredAt, StringComparer.Ordinal)
                .Select(p => new RankingPlayerEntry(p.name, p.grade, FormatDate(p.acquiredAt))), hasMore);
        }

        async Task LoadGradeAsync(int grade, bool reset) {
            if (disposed || loadingGrade[grade]) return;
            loadingGrade[grade] = true;
            try {
                var page = await records.GetRankingAsync(grade, reset ? "" : cursors[grade] ?? "");
                if (disposed) return;
                // Grades never decrease. Concurrent grade pages can describe the same player before and after a promotion.
                foreach (var profile in page.items) {
                    if (ranking.Any(current => current.name == profile.name && current.grade > profile.grade)) continue;
                    ranking.RemoveAll(current => current.name == profile.name);
                    ranking.Add(profile);
                }
                cursors[grade] = page.nextCursor ?? ""; hasMore[grade] = !string.IsNullOrEmpty(cursors[grade]);
                failedGrade[grade] = false; PresentRanking(); profiles.CacheRanking(ranking);
            } catch (Exception exception) {
                failedGrade[grade] = true;
                if (!disposed) {
                    hasMore[grade] = !string.IsNullOrEmpty(cursors[grade]) || cursors[grade] == null;
                    PresentRanking();
                    Debug.Log($"[Ranking] {exception.Message}");
                }
            } finally {
                loadingGrade[grade] = false;
                if (!disposed) view.SetRankingStatus(failedGrade.Any(failed => failed) ? "オフライン：保存済みの階級を含めて表示しています" : loadingGrade.Any(loading => loading) ? "階級を読み込んでいます…" : "");
            }
        }

        async Task LoadPlayerHistoryAsync(string name, bool append) {
            if (disposed || inputState != RankingInputState.Ready || historyLoading) return;
            historyLoading = true; selectedPlayer = name;
            var operation = ++historyOperation;
            var rowPosition = append ? historyListView.RowPosition : 0;
            if (!append) { histories.Clear(); historyCursor = ""; historyListView.SetEntries(histories); }
            view.ShowHistory(name, false); historyListView.ShowStatus("読み込み中", "対戦履歴を取得しています");
            try {
                var page = await records.GetHistoryAsync(name, append ? historyCursor : "");
                if (disposed || operation != historyOperation) return;
                foreach (var entry in page.items.Select(ToEntry)) if (!histories.Any(row => row.Id == entry.Id)) histories.Add(entry);
                historyCursor = page.nextCursor; historyListView.SetEntries(histories, rowPosition); view.ShowHistory(name, !string.IsNullOrEmpty(historyCursor));
            } catch (Exception exception) {
                if (!disposed && operation == historyOperation) {
                    view.ShowHistory(name, !string.IsNullOrEmpty(historyCursor));
                    historyListView.ShowStatus("通信できませんでした", append && !string.IsNullOrEmpty(historyCursor) ? "「さらに表示」で再試行できます" : "一度閉じて、もう一度選んでください");
                    Debug.Log($"[Ranking] {exception.Message}");
                }
            } finally { if (operation == historyOperation) historyLoading = false; }
        }

        async Task RequestReplayAsync(string id) {
            if (disposed || inputState != RankingInputState.Ready || string.IsNullOrWhiteSpace(id)) {
                return;
            }

            inputState = RankingInputState.Transitioning;
            try {
                var detail = await battleHistoryApiClient.GetDetailAsync(id);
                if (disposed) return;
                if (!CanPlayReplay(detail?.replayPayload)) {
                    Debug.LogWarning($"{LOG_PREFIX} Replay is unavailable or incompatible. id={id}");
                    inputState = RankingInputState.Ready;
                    return;
                }

                navigation.PlayerName = selectedPlayer; navigation.HistoryCursor = historyCursor;
                navigation.ListPosition = view.ListPosition; navigation.HistoryRowPosition = historyListView.RowPosition; navigation.Entries = histories.ToArray();
                navigation.CaptureRanking(ranking, cursors);
                session.SetMode(BattlePlayMode.Replay);
                session.Prepare(detail.participants is { Length: 2 } ? detail.participants : Enumerable.Range(0, 2).Select(slot => new PlayerIdentity { name = GetArrayValue(detail.playerNames, slot, "ゲスト"), grade = -1 }).ToArray());
                replaySetting.SetReplay(detail.replayPayload);
                appNetworkSetting.SetLocalOnlinePlayerId(0);
                ApplyReplaySelections(detail.replayPayload);
                var nextScene = ResolveBattleScene(detail.replayPayload.stage);
                var result = sceneTransitionService.RequestStartTransition(nextScene);
                Debug.Log($"{LOG_PREFIX} RequestReplay transition result. nextScene={nextScene}, isSuccess={result.IsSuccess}");
                if (!result.IsSuccess) {
                    replaySetting.ClearReplay();
                    inputState = RankingInputState.Ready;
                }
            }
            catch (Exception exception) {
                if (disposed) return;
                Debug.LogWarning($"{LOG_PREFIX} Failed to start replay: {exception.Message}");
                replaySetting?.ClearReplay();
                inputState = RankingInputState.Ready;
            }
        }

        void RequestTransitionToMenu() {
            if (inputState != RankingInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu ignored because inputState={inputState}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu requesting start transition. nextScene={AppScene.Menu}");
            navigation.Clear();
            session.SetMode(BattlePlayMode.Local);
            RequestTransition(AppScene.Menu);
        }

        void RequestTransition(AppScene nextScene) {
            inputState = RankingInputState.Transitioning;
            var result = sceneTransitionService.RequestStartTransition(nextScene);
            Debug.Log($"{LOG_PREFIX} RequestTransition result. nextScene={nextScene}, isSuccess={result.IsSuccess}");
            if (result.IsSuccess) {
                return;
            }

            inputState = RankingInputState.Ready;
        }

        void ApplyReplaySelections(ReplayPayload replayPayload) {
            if (Enum.TryParse<Stage>(replayPayload.stage, out var stage)) {
                battleSelectSetting.SelectStage(stage);
            }

            battleSelectSetting.SelectMusic(replayPayload.musicId);
            playerSelectSetting.ResetSelections();
            for (var playerId = 0; playerId < replayPayload.strikerIds.Length; playerId++) {
                playerSelectSetting.SelectStriker(playerId, (Striker)replayPayload.strikerIds[playerId]);
            }
        }

        bool CanPlayReplay(ReplayPayload replay) {
            return replay != null && replay.schemaVersion == 1
                && Enum.TryParse<Stage>(replay.stage, out var stage) && Enum.IsDefined(typeof(Stage), stage)
                && !string.IsNullOrWhiteSpace(replay.musicId) && musicRegistry.GetAll().Any(music => music.Id == replay.musicId)
                && replay.strikerIds != null && replay.strikerIds.Length == 2
                && replay.strikerIds.All(id => strikerRegistry.GetAll().Any(striker => (int)striker.BattleStriker == id))
                && replay.rounds != null && replay.rounds.Length > 0
                && replay.rounds.All(round => round != null
                    && (round.beatNotifications == null || round.beatNotifications.All(item => item != null))
                    && (round.preBeatStates == null || round.preBeatStates.All(item => item != null)));
        }

        RankingBattleHistoryEntry ToEntry(BattleHistorySummary summary) {
            var playerA = GetArrayValue(summary.playerNames, 0, "ゲスト");
            var playerB = GetArrayValue(summary.playerNames, 1, "ゲスト");
            var strikerA = GetArrayValue(summary.strikerNames, 0, "");
            var strikerB = GetArrayValue(summary.strikerNames, 1, "");
            var winner = summary.winnerPlayerId >= 0 ? $"P{summary.winnerPlayerId + 1} WIN" : "DRAW";
            var battle = $"{summary.stage} / {summary.musicName} / {strikerA} vs {strikerB}";
            return new RankingBattleHistoryEntry(
                summary.id,
                playerA,
                playerB,
                FormatDate(summary.playedAt),
                winner,
                battle,
                summary.hasReplay && !string.IsNullOrWhiteSpace(summary.musicId)
                    && musicRegistry.GetAll().Any(music => music.Id == summary.musicId));
        }

        static string GetArrayValue(string[] values, int index, string fallback) {
            return values != null && index >= 0 && index < values.Length && !string.IsNullOrWhiteSpace(values[index])
                ? values[index]
                : fallback;
        }

        static string FormatDate(string value) {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime)) {
                return dateTime.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
            }

            return value ?? "";
        }

        static AppScene ResolveBattleScene(string stage) {
            return stage == Stage.Street.ToString()
                ? AppScene.Street
                : AppScene.Live;
        }

        public void Dispose() {
            disposed = true;
            subscriptions.Dispose();
        }
    }
}
