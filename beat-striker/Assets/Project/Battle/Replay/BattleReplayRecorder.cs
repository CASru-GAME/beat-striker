using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class BattleReplayRecorder : IDisposable {
        const string LOG_PREFIX = "[BattleReplayRecorder]";
        const int PlayerCount = 2;
        const float PreBeatSnapshotLeadSeconds = 0.05f;

        readonly IBattleSelectSetting battleSelectSetting;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IStageRegistry stageRegistry;
        readonly IMusicRegistry musicRegistry;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly IBeatjudge beatJudge;
        readonly IMusicPlayer musicPlayer;
        readonly IStrikerRegistry strikerRegistry;
        readonly IBattleHistoryApiClient apiClient;
        readonly IReplaySetting replaySetting;
        readonly IAISetting aiSetting;
        readonly List<ReplayRoundBuilder> rounds = new();
        readonly List<IDisposable> subscriptions = new();
        readonly List<IDisposable> roundSubscriptions = new();
        readonly HashSet<(int Round, int BeatIndex, int PlayerId)> recordedPreBeatSnapshots = new();
        BattleHistorySaveRequest saveRequest;
        ReplayRoundBuilder currentRound;
        bool battleStarted;
        bool finished;

        [Inject]
        public BattleReplayRecorder(
            IBattleSelectSetting battleSelectSetting,
            IPlayerSelectSetting playerSelectSetting,
            IStageRegistry stageRegistry,
            IMusicRegistry musicRegistry,
            IAppStrikerRegistry appStrikerRegistry,
            IBeatjudge beatJudge,
            IMusicPlayer musicPlayer,
            IStrikerRegistry strikerRegistry,
            IBattleHistoryApiClient apiClient,
            IReplaySetting replaySetting,
            IAISetting aiSetting) {
            this.battleSelectSetting = battleSelectSetting;
            this.playerSelectSetting = playerSelectSetting;
            this.stageRegistry = stageRegistry;
            this.musicRegistry = musicRegistry;
            this.appStrikerRegistry = appStrikerRegistry;
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
            this.strikerRegistry = strikerRegistry;
            this.apiClient = apiClient;
            this.replaySetting = replaySetting;
            this.aiSetting = aiSetting;
        }

        public void BeginBattle() {
            if (ShouldSkipHistoryRecording()) {
                return;
            }

            battleStarted = true;
            finished = false;
            rounds.Clear();
            recordedPreBeatSnapshots.Clear();
            saveRequest = BuildInitialRequest();
            subscriptions.Add(Observable.EveryUpdate().Subscribe(_ => UpdatePreBeatSnapshots()));
        }

        public void BeginRound(int roundNumber) {
            if (!battleStarted || ShouldSkipHistoryRecording()) {
                return;
            }

            currentRound = new ReplayRoundBuilder(roundNumber, musicPlayer.CurrentPlaybackTime);
            rounds.Add(currentRound);
            DisposeRoundSubscriptions();
            for (var playerId = 0; playerId < PlayerCount; playerId++) {
                var capturedPlayerId = playerId;
                var beatPlayer = beatJudge.GetBeatPlayer(capturedPlayerId);
                roundSubscriptions.Add(beatPlayer.OnBeatCommandRequested.Subscribe(result => RecordBeatResult(capturedPlayerId, result, OnlineBeatNotificationKind.Command)));
                roundSubscriptions.Add(beatPlayer.OnBeatPassed.Subscribe(result => RecordBeatResult(capturedPlayerId, result, OnlineBeatNotificationKind.Pass)));
            }
        }

        public void FinishBattle(int winnerPlayerId, IReadOnlyDictionary<PlayerId, int> roundWins) {
            if (!battleStarted || finished || ShouldSkipHistoryRecording() || saveRequest == null) {
                return;
            }

            finished = true;
            saveRequest.winnerPlayerId = winnerPlayerId;
            saveRequest.roundWinCounts = BuildRoundWinCounts(roundWins);
            saveRequest.playedAt = DateTime.UtcNow.ToString("O");
            saveRequest.replayPayload.rounds = rounds.Select(round => round.Build()).ToArray();
            StopRecording();
            _ = SaveAsync(saveRequest);
        }

        async Task SaveAsync(BattleHistorySaveRequest request) {
            try {
                await apiClient.SaveAsync(request);
                Debug.Log($"<color=cyan>{LOG_PREFIX} Battle history upload succeeded.</color>");
            }
            catch (Exception exception) {
                Debug.Log($"<color=red>{LOG_PREFIX} Failed to upload battle history (continuing): {exception.Message}</color>");
            }
        }

        BattleHistorySaveRequest BuildInitialRequest() {
            var selectedStage = battleSelectSetting.SelectedStage.CurrentValue;
            var selectedMusicId = battleSelectSetting.SelectedMusicId.CurrentValue;
            var musicInfo = musicRegistry.GetById(selectedMusicId);
            var strikerIds = new int[PlayerCount];
            var strikerNames = new string[PlayerCount];
            for (var playerId = 0; playerId < PlayerCount; playerId++) {
                if (!playerSelectSetting.TryGetStriker(playerId, out var striker)) {
                    striker = appStrikerRegistry.Default.BattleStriker;
                }

                strikerIds[playerId] = (int)striker;
                strikerNames[playerId] = appStrikerRegistry.GetByStriker(striker).DisplayName;
            }

            return new BattleHistorySaveRequest {
                playerNames = new[] { "ゲスト", "ゲスト" },
                stage = selectedStage.ToString(),
                stageName = stageRegistry.GetByStage(selectedStage).DisplayName,
                musicId = selectedMusicId,
                musicName = musicInfo.DisplayName,
                strikerNames = strikerNames,
                strikerIds = strikerIds,
                winnerPlayerId = -1,
                roundWinCounts = new int[PlayerCount],
                playedAt = DateTime.UtcNow.ToString("O"),
                appVersion = Application.version,
                replayPayload = new ReplayPayload {
                    schemaVersion = 1,
                    stage = selectedStage.ToString(),
                    musicId = selectedMusicId,
                    strikerIds = strikerIds,
                    appVersion = Application.version,
                    rounds = Array.Empty<ReplayRoundPayload>(),
                }
            };
        }

        void RecordBeatResult(int playerId, IBeatPlayer.BeatResult result, OnlineBeatNotificationKind fallbackKind) {
            if (currentRound == null || result.BeatIndex < 0) {
                return;
            }

            var kind = result.IsSuccess ? OnlineBeatNotificationKind.Command : fallbackKind;
            currentRound.BeatNotifications.Add(new ReplayBeatNotificationPayload {
                playerId = playerId,
                beatIndex = result.BeatIndex,
                time = kind == OnlineBeatNotificationKind.Pass ? Mathf.Max(0f, result.Time - 0.03f) : result.Time,
                kind = (int)kind,
                zone = (int)result.Zone,
                button = (int)result.Button,
                directionX = result.Direction.x,
                directionY = result.Direction.y,
            });
        }

        void UpdatePreBeatSnapshots() {
            if (currentRound == null || musicPlayer.CurrentBeatTimeline.Length == 0) {
                return;
            }

            var playbackTime = musicPlayer.CurrentPlaybackTime;
            var beatTimeline = musicPlayer.CurrentBeatTimeline;
            for (var beatIndex = 0; beatIndex < beatTimeline.Length; beatIndex++) {
                if (beatTimeline[beatIndex] < currentRound.StartPlaybackTime - 0.001f) {
                    continue;
                }

                var captureTime = beatTimeline[beatIndex] - PreBeatSnapshotLeadSeconds;
                if (playbackTime < captureTime) {
                    if (captureTime > playbackTime + 0.25f) {
                        break;
                    }
                    continue;
                }

                for (var playerId = 0; playerId < PlayerCount; playerId++) {
                    if (!recordedPreBeatSnapshots.Add((currentRound.RoundNumber, beatIndex, playerId))) {
                        continue;
                    }

                    if (!strikerRegistry.Get(playerId).TryGetValue(out var striker)) {
                        continue;
                    }

                    var snapshot = striker.BuildPreBeatStateSnapshot(beatIndex, 0f);
                    currentRound.PreBeatStates.Add(new ReplayPreBeatStatePayload {
                        playerId = playerId,
                        applyBeatIndex = beatIndex,
                        hitPoint = snapshot.HitPoint,
                        specialPoint = snapshot.SpecialPoint,
                        position = snapshot.Position,
                        statePathId = snapshot.StatePathId,
                        playbackTime = playbackTime,
                    });
                }
            }
        }

        static int[] BuildRoundWinCounts(IReadOnlyDictionary<PlayerId, int> roundWins) {
            var counts = new int[PlayerCount];
            for (var playerId = 0; playerId < PlayerCount; playerId++) {
                counts[playerId] = roundWins != null && roundWins.TryGetValue(new PlayerId(playerId), out var count) ? count : 0;
            }
            return counts;
        }

        bool ShouldSkipHistoryRecording() {
            return replaySetting.HasReplay || aiSetting.IsInfiniteRoundMode;
        }

        public void Dispose() {
            StopRecording();
        }

        void StopRecording() {
            foreach (var subscription in subscriptions) {
                subscription.Dispose();
            }
            subscriptions.Clear();
            DisposeRoundSubscriptions();
            currentRound = null;
        }

        void DisposeRoundSubscriptions() {
            foreach (var subscription in roundSubscriptions) {
                subscription.Dispose();
            }
            roundSubscriptions.Clear();
        }

        class ReplayRoundBuilder {
            public int RoundNumber { get; }
            public float StartPlaybackTime { get; }
            public List<ReplayBeatNotificationPayload> BeatNotifications { get; } = new();
            public List<ReplayPreBeatStatePayload> PreBeatStates { get; } = new();

            public ReplayRoundBuilder(int roundNumber, float startPlaybackTime) {
                RoundNumber = roundNumber;
                StartPlaybackTime = startPlaybackTime;
            }

            public ReplayRoundPayload Build() {
                return new ReplayRoundPayload {
                    roundNumber = RoundNumber,
                    beatNotifications = BeatNotifications
                        .OrderBy(item => item.time)
                        .ThenBy(item => item.playerId)
                        .ToArray(),
                    preBeatStates = PreBeatStates
                        .OrderBy(item => item.playbackTime)
                        .ThenBy(item => item.playerId)
                        .ToArray(),
                };
            }
        }
    }
}
