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
        readonly CompositeDisposable subscriptions = new();
        RankingInputState inputState = RankingInputState.Ready;

        [Inject]
        public RankingPresenter(
            RankingPresenterView view,
            RankingHistoryListView historyListView,
            ISceneTransitionService sceneTransitionService,
            IBattleHistoryApiClient battleHistoryApiClient,
            IReplaySetting replaySetting,
            IBattleSelectSetting battleSelectSetting,
            IPlayerSelectSetting playerSelectSetting,
            IAppNetworkSetting appNetworkSetting) {
            this.view = view;
            this.historyListView = historyListView;
            this.sceneTransitionService = sceneTransitionService;
            this.battleHistoryApiClient = battleHistoryApiClient;
            this.replaySetting = replaySetting;
            this.battleSelectSetting = battleSelectSetting;
            this.playerSelectSetting = playerSelectSetting;
            this.appNetworkSetting = appNetworkSetting;
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
            Debug.Log($"{LOG_PREFIX} EnterRankingAsync requesting end transition. scene={AppScene.Ranking}");
            var result = await sceneTransitionService.RequestEndTransitionAsync(AppScene.Ranking);
            Debug.Log($"{LOG_PREFIX} EnterRankingAsync end transition completed. isSuccess={result.IsSuccess}");
            await LoadHistoryAsync();
        }

        async Task LoadHistoryAsync() {
            try {
                var summaries = await battleHistoryApiClient.GetSummariesAsync(50);
                historyListView.SetEntries(summaries.Select(ToEntry));
            }
            catch (Exception exception) {
                Debug.LogWarning($"{LOG_PREFIX} Failed to load battle histories: {exception.Message}");
                historyListView.SetEntries(new[] {
                    new RankingBattleHistoryEntry(string.Empty, "履歴取得失敗", "", "", "", "しばらくしてから再度開いてください", false)
                });
            }
        }

        async Task RequestReplayAsync(string id) {
            if (inputState != RankingInputState.Ready || string.IsNullOrWhiteSpace(id)) {
                return;
            }

            inputState = RankingInputState.Transitioning;
            try {
                var detail = await battleHistoryApiClient.GetDetailAsync(id);
                if (detail?.replayPayload == null) {
                    inputState = RankingInputState.Ready;
                    return;
                }

                replaySetting.SetReplay(detail.replayPayload);
                appNetworkSetting.SetLocalOnlinePlayerId(0);
                ApplyReplaySelections(detail.replayPayload);
                var nextScene = ResolveBattleScene(detail.replayPayload.stage);
                var result = sceneTransitionService.RequestStartTransition(nextScene);
                Debug.Log($"{LOG_PREFIX} RequestReplay transition result. nextScene={nextScene}, isSuccess={result.IsSuccess}");
                if (!result.IsSuccess) {
                    inputState = RankingInputState.Ready;
                }
            }
            catch (Exception exception) {
                Debug.LogWarning($"{LOG_PREFIX} Failed to start replay: {exception.Message}");
                inputState = RankingInputState.Ready;
            }
        }

        void RequestTransitionToMenu() {
            if (inputState != RankingInputState.Ready) {
                Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu ignored because inputState={inputState}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} RequestTransitionToMenu requesting start transition. nextScene={AppScene.Menu}");
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

        static RankingBattleHistoryEntry ToEntry(BattleHistorySummary summary) {
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
                summary.hasReplay);
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
            subscriptions.Dispose();
        }
    }
}
