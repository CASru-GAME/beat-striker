
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
 
using App;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {

    public record BeatPlayerBattleResult(int Score, int Excellent, int Good, int Miss, int MaxCombo);

    public interface IBeatjudge {
        IBeatPlayer GetBeatPlayer(int playerId);
        IReadOnlyDictionary<PlayerId, BeatPlayerBattleResult> GetBattleResults();
        void ResetBattleState();
        void ResetRoundState();
        void Pause();
        void Resume();
        /// <summary>オンライン: 双方のサスペンド要求が揃った拍で呼ばれる（引数は適用ビートインデックス）。</summary>
        void SetOnlineDualSuspendMenuPauseHandler(Action<int> handler);
    }

    public partial class BeatJudge : IBeatjudge, IDisposable {
        public void SetOnlineDualSuspendMenuPauseHandler(Action<int> handler) {
            onlineDualSuspendMenuPauseHandler = handler;
        }

        // ポーズ要求を送る「次のオンライン適用拍」。オンライン処理中は lastOnlineBeatIndex 基準、未開始は再生位置から算出。
        public int GetSuspendMenuApplyBeatIndex() {
            return lastOnlineBeatIndex >= 0
                ? lastOnlineBeatIndex + 1
                : musicPlayer.JudgeTiming(musicPlayer.CurrentPlaybackTime).BeatIndex;
        }

        const string LOG_PREFIX = "[BeatJudge]";
        const int PLAYER_COUNT = 2;
        const float PRE_BEAT_STATE_APPLY_OFFSET_SECONDS = 0.02f;
        static readonly float[] PreBeatStatePublishOffsetsSeconds = { 0.2f, 0.15f, 0.1f, 0.05f };

        readonly IAudioSetting audioSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IBattleOnlineSync battleOnlineSync;
        readonly BeatOnlineCommandBuffer onlineCommandBuffer;
        readonly IStrikerRegistry strikerRegistry;
        readonly IMusicPlayer musicPlayer;
        readonly List<IDisposable> subscriptions = new();
        readonly Queue<IMusicPlayer.BeatSignal> pendingOnlineBeatSignals = new();
        readonly HashSet<PreBeatStatePublishKey> publishedPreBeatStateSnapshotKeys = new();
        readonly HashSet<int> appliedPreBeatStateSnapshotBeats = new();
        readonly int[] lastReceivedOnlineBeatIndexByPlayer = new int[PLAYER_COUNT];
        BeatPlayer[] beatPlayer = new BeatPlayer[PLAYER_COUNT];
        float lastCommandPlaybackTime = -1f;
        int lastOnlineBeatIndex = -1;
        bool isOnlineBeatDrainRunning;
        bool isPaused;
        bool isOnlineNotificationWaitPaused;
        float timeScaleBeforeOnlineNotificationWait = 1f;
        // オンライン専用: 指定拍にサスペンド要求が届いたら BattleFlow 側へ通知（ポーズ＋FlowGate SuspendMenuBeatBarrier）。
        Action<int> onlineDualSuspendMenuPauseHandler;

        record PreBeatStatePublishKey(int BeatIndex, int SlotIndex);

        [Inject]
        public BeatJudge(IGamePadRegistry gamePadRegistry, IMusicPlayer musicPlayer, IAudioSetting audioSetting,
            IAppNetworkSetting appNetworkSetting, IBattleOnlineSync battleOnlineSync,
            BeatOnlineCommandBuffer onlineCommandBuffer, IStrikerRegistry strikerRegistry) {
            this.audioSetting = audioSetting;
            this.appNetworkSetting = appNetworkSetting;
            this.battleOnlineSync = battleOnlineSync;
            this.onlineCommandBuffer = onlineCommandBuffer;
            this.strikerRegistry = strikerRegistry;
            this.musicPlayer = musicPlayer;


            for (int i = 0; i < beatPlayer.Length; i++) {
                beatPlayer[i] = new BeatPlayer();
                lastReceivedOnlineBeatIndexByPlayer[i] = -1;
            }

            for (int i = 0; i < beatPlayer.Length; i++) {
                var playerIndex = i;
                var gamePad = gamePadRegistry.Get(playerIndex);

                subscriptions.Add(gamePad.OnDirection.Subscribe(direction => {
                    beatPlayer[playerIndex].UpdateInputDirection(direction);
                }));

                subscriptions.Add(gamePad.OnDirectionCanceled.Subscribe(_ => {
                    beatPlayer[playerIndex].ClearInputDirection();
                }));

                var subscription = gamePad.OnButtonDown.Subscribe(button => {
                    if (isPaused || isOnlineNotificationWaitPaused) {
                        return;
                    }

                    if (musicPlayer.CurrentBeatTimeline.Length == 0) {
                        return;
                    }

                    var player = beatPlayer[playerIndex];
                    var time = musicPlayer.CurrentPlaybackTime;
                    if (lastCommandPlaybackTime >= 0f && time < lastCommandPlaybackTime) {
                        for (var j = 0; j < beatPlayer.Length; j++) {
                            beatPlayer[j].ResetForLoop();
                        }

                        ResetOnlineCommandState();
                    }

                    lastCommandPlaybackTime = time;

                    if (player.IsInputLocked) {
                        return;
                    }

                    var result = musicPlayer.JudgeTiming(time);
                    var isTimingSuccess = result.Zone != BeatJudgeZone.Miss && time < result.BeatTime;
                    if (!isTimingSuccess) {
                        player.onBeatCommandRequested.OnNext(new IBeatPlayer.BeatResult(
                            result.BeatIndex,
                            time,
                            false,
                            BeatJudgeZone.Miss,
                            button,
                            player.CurrentInputDirection,
                            player.ComboCount.CurrentValue));
                        return;
                    }

                    var isGood = player.TrySavePendingCommand(result.BeatIndex, result.Zone, button,
                        player.CurrentInputDirection);

                    if (isGood) {
                        player.LockInputUntilBeat(result.BeatIndex);
                        var beatResult = new IBeatPlayer.BeatResult(result.BeatIndex, time, true, result.Zone, button,
                            player.CurrentInputDirection, player.ComboCount.CurrentValue);
                        player.onBeatCommandRequested.OnNext(beatResult);
                        SubmitLocalOnlineCommandIfNeeded(playerIndex, beatResult);
                    }

                    // Record that player attempted this beat so it's not considered a pass later
                    player.RecordAttempt(result.BeatIndex);
                });
                subscriptions.Add(subscription);
            }

            subscriptions.Add(battleOnlineSync.OnBeatNotificationReceived.Subscribe(ApplyOnlineBeatNotification));

            subscriptions.Add(Observable.EveryUpdate().Subscribe(_ => {
                UpdateOnlinePreBeatStateSnapshots();
            }));

            subscriptions.Add(musicPlayer.OnBeatTiming.Subscribe(signal => {
                if (isPaused) {
                    return;
                }

                if (IsOnlineBattle()) {
                    EnqueueOnlineBeatSignal(signal);
                    return;
                }

                for (var playerIndex = 0; playerIndex < beatPlayer.Length; playerIndex++) {
                    if (!beatPlayer[playerIndex].TryConsumePendingCommand(signal.BeatIndex, out var zone,
                            out var button, out var direction)) {
                        // If player attempted this beat (but it wasn't saved as pending), it's a miss rather than a pass
                        if (beatPlayer[playerIndex].HasAttempt(signal.BeatIndex)) {
                            beatPlayer[playerIndex].ClearAttempt(signal.BeatIndex);
                            beatPlayer[playerIndex].ResetCombo();
                            beatPlayer[playerIndex].IncrementMiss();
                            beatPlayer[playerIndex].onBeatCommandExecuted.OnNext(
                                new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, false, BeatJudgeZone.Miss,
                                    default, Vector2.zero, beatPlayer[playerIndex].ComboCount.CurrentValue));
                            continue;
                        }

                        // No pending command and no attempt -> player passed the beat
                        beatPlayer[playerIndex].ResetCombo();
                        beatPlayer[playerIndex].IncrementMiss();
                        beatPlayer[playerIndex].onBeatPassed.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex,
                            signal.BeatTime, false, BeatJudgeZone.Miss, default,
                            beatPlayer[playerIndex].CurrentInputDirection,
                            beatPlayer[playerIndex].ComboCount.CurrentValue));
                        continue;
                    }

                    beatPlayer[playerIndex].UnlockInputIfBeatMatched(signal.BeatIndex);
                    beatPlayer[playerIndex].IncrementCombo();
                    if (zone == BeatJudgeZone.Excellent) {
                        beatPlayer[playerIndex].IncrementExcellent();
                    }
                    else if (zone == BeatJudgeZone.Good) {
                        beatPlayer[playerIndex].IncrementGood();
                    }

                    beatPlayer[playerIndex].AddScore(CalculateScore(zone));
                    beatPlayer[playerIndex].onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex,
                        signal.BeatTime, true, zone, button, direction,
                        beatPlayer[playerIndex].ComboCount.CurrentValue));
                }
            }));
        }

        void SubmitLocalOnlineCommandIfNeeded(int playerId, IBeatPlayer.BeatResult result) {
            if (!IsOnlineBattle() || playerId != ResolveLocalOnlinePlayerId() || result.BeatIndex < 0 ||
                !result.IsSuccess) {
                return;
            }

            var notification = new OnlineBeatNotificationSnapshot(
                0,
                playerId,
                result.BeatIndex,
                result.Time,
                OnlineBeatNotificationKind.Command,
                result.Zone,
                result.Button,
                result.Direction);
            if (onlineCommandBuffer.TrySubmit(notification)) {
                battleOnlineSync.PublishBeatNotification(notification);
            }
        }

        void ApplyOnlineBeatNotification(OnlineBeatNotificationSnapshot notification) {
            if (!IsOnlineBattle()
                || notification.PlayerId == ResolveLocalOnlinePlayerId()
                || notification.PlayerId < 0
                || notification.PlayerId >= beatPlayer.Length) {
                return;
            }

            FillMissingRemoteBeatNotificationsIfNeeded(notification.PlayerId, notification.BeatIndex);
            if (!onlineCommandBuffer.TrySubmit(notification)) {
                return;
            }

            lastReceivedOnlineBeatIndexByPlayer[notification.PlayerId] = Mathf.Max(
                lastReceivedOnlineBeatIndexByPlayer[notification.PlayerId],
                notification.BeatIndex);
            Debug.Log(
                $"{LOG_PREFIX} Applied remote online notification. player={notification.PlayerId}, beat={notification.BeatIndex}, kind={notification.Kind}, ready={onlineCommandBuffer.IsReady(notification.BeatIndex, PLAYER_COUNT)}");
            var player = beatPlayer[notification.PlayerId];
            if (notification.Kind == OnlineBeatNotificationKind.Command) {
                player.onBeatCommandRequested.OnNext(new IBeatPlayer.BeatResult(
                    notification.BeatIndex,
                    notification.Time,
                    true,
                    notification.Zone,
                    notification.Button,
                    notification.Direction,
                    player.ComboCount.CurrentValue));
            }
        }

        async Task ProcessOnlineBeatAsync(IMusicPlayer.BeatSignal signal) {
            if (lastOnlineBeatIndex >= 0 && signal.BeatIndex < lastOnlineBeatIndex) {
                ResetOnlineCommandState();
            }

            lastOnlineBeatIndex = signal.BeatIndex;

            SubmitLocalOnlinePassIfNeeded(signal);
            if (isPaused || !IsOnlineBattle()) {
                return;
            }

            if (!onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)) {
                var remotePlayerId = ResolveRemoteOnlinePlayerId();
                if (!onlineCommandBuffer.HasSubmissionAfter(signal.BeatIndex, remotePlayerId)) {
                    await WaitForOnlineBeatNotificationAsync(signal);
                }

                if (isPaused || !IsOnlineBattle()) {
                    return;
                }

                if (!onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)) {
                    Debug.Log($"{LOG_PREFIX} Skipped online beat because notification table is not ready. beat={signal.BeatIndex}");
                    return;
                }
            }

            ApplyStrikerPreBeatStateSnapshotIfNeeded(signal.BeatIndex);

            for (var playerId = 0; playerId < PLAYER_COUNT; playerId++) {
                if (onlineCommandBuffer.TryGetNotification(signal.BeatIndex, playerId, out var notification)) {
                    ExecuteOnlineNotification(playerId, notification, signal);
                }
            }

            // 打鍵コマンド適用の直後・CloseBeat の直前: この拍のサスペンド要求があれば一度だけポーズ＋ゲートへ進める。
            if (IsOnlineBattle()
                && onlineDualSuspendMenuPauseHandler != null
                && battleOnlineSync.TryConsumeSuspendMenuRequest(signal.BeatIndex)) {
                onlineDualSuspendMenuPauseHandler(signal.BeatIndex);
            }

            onlineCommandBuffer.CloseBeat(signal.BeatIndex);
            CleanupPreBeatStateTrackingBefore(signal.BeatIndex + 1);
            battleOnlineSync.ClearStrikerPreBeatStateSnapshotsBefore(signal.BeatIndex + 1);
            Debug.Log($"{LOG_PREFIX} Completed online beat. beat={signal.BeatIndex}");
        }

        void EnqueueOnlineBeatSignal(IMusicPlayer.BeatSignal signal) {
            pendingOnlineBeatSignals.Enqueue(signal);
            if (isOnlineBeatDrainRunning) {
                return;
            }

            isOnlineBeatDrainRunning = true;
            _ = DrainOnlineBeatSignalsAsync();
        }

        async Task DrainOnlineBeatSignalsAsync() {
            try {
                while (pendingOnlineBeatSignals.Count > 0) {
                    var signal = pendingOnlineBeatSignals.Dequeue();
                    await ProcessOnlineBeatAsync(signal);
                }
            }
            finally {
                isOnlineBeatDrainRunning = false;
                if (pendingOnlineBeatSignals.Count > 0 && !isOnlineBeatDrainRunning) {
                    isOnlineBeatDrainRunning = true;
                    _ = DrainOnlineBeatSignalsAsync();
                }
            }
        }

        async Task WaitForOnlineBeatNotificationAsync(IMusicPlayer.BeatSignal signal) {
            Debug.Log(
                $"{LOG_PREFIX} Waiting online beat notification. beat={signal.BeatIndex}, localPlayer={ResolveLocalOnlinePlayerId()}, isHost={battleOnlineSync.IsSessionHost}");
            isOnlineNotificationWaitPaused = true;
            timeScaleBeforeOnlineNotificationWait = Time.timeScale;
            musicPlayer.Pause();
            Time.timeScale = 0f;
            try {
                while (!isPaused
                       && IsOnlineBattle()
                       && !onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)) {
                    await Task.Yield();
                }
            }
            finally {
                Time.timeScale = timeScaleBeforeOnlineNotificationWait;
                isOnlineNotificationWaitPaused = false;
                if (!isPaused && IsOnlineBattle() && onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)) {
                    musicPlayer.SyncPlaybackTime(signal.BeatTime);
                    musicPlayer.Resume();
                }
            }
        }

        void SubmitLocalOnlinePassIfNeeded(IMusicPlayer.BeatSignal signal) {
            var localPlayerId = ResolveLocalOnlinePlayerId();
            if (onlineCommandBuffer.HasSubmission(signal.BeatIndex, localPlayerId)) {
                return;
            }

            var notification = CreatePassNotification(localPlayerId, signal);
            if (onlineCommandBuffer.TrySubmit(notification)) {
                Debug.Log(
                    $"{LOG_PREFIX} Submitted local online pass. player={localPlayerId}, beat={signal.BeatIndex}, ready={onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)}");
                battleOnlineSync.PublishBeatNotification(notification);
            }
        }

        void UpdateOnlinePreBeatStateSnapshots() {
            if (isPaused || !IsOnlineBattle()) {
                return;
            }

            var playbackTime = musicPlayer.CurrentPlaybackTime;
            var beatTimeline = musicPlayer.CurrentBeatTimeline;
            for (var beatIndex = 0; beatIndex < beatTimeline.Length; beatIndex++) {
                var beatTime = beatTimeline[beatIndex];
                if (beatTime + 0.001f < playbackTime) {
                    continue;
                }

                for (var slotIndex = 0; slotIndex < PreBeatStatePublishOffsetsSeconds.Length; slotIndex++) {
                    var publishTime = beatTime - PreBeatStatePublishOffsetsSeconds[slotIndex];
                    var key = new PreBeatStatePublishKey(beatIndex, slotIndex);
                    if (playbackTime >= publishTime && publishedPreBeatStateSnapshotKeys.Add(key)) {
                        PublishLocalStrikerPreBeatStateSnapshot(beatIndex);
                    }
                }

                if (playbackTime >= beatTime - PRE_BEAT_STATE_APPLY_OFFSET_SECONDS) {
                    ApplyStrikerPreBeatStateSnapshotIfNeeded(beatIndex);
                }

                if (beatTime - PreBeatStatePublishOffsetsSeconds[0] > playbackTime + 0.25f) {
                    break;
                }
            }
        }

        void PublishLocalStrikerPreBeatStateSnapshot(int beatIndex) {
            var localPlayerId = ResolveLocalOnlinePlayerId();
            if (!strikerRegistry.Get(localPlayerId).TryGetValue(out var striker)) {
                return;
            }

            var sentNetworkTime = battleOnlineSync.NetworkTime;
            battleOnlineSync.PublishStrikerPreBeatStateSnapshot(
                striker.BuildPreBeatStateSnapshot(beatIndex, sentNetworkTime));
        }

        void ApplyStrikerPreBeatStateSnapshotIfNeeded(int beatIndex) {
            if (!appliedPreBeatStateSnapshotBeats.Add(beatIndex)) {
                return;
            }

            var localPlayerId = ResolveLocalOnlinePlayerId();
            for (var playerId = 0; playerId < PLAYER_COUNT; playerId++) {
                if (playerId == localPlayerId) {
                    continue;
                }

                if (!battleOnlineSync.TryGetLatestStrikerPreBeatStateSnapshot(beatIndex, playerId, out var snapshot)) {
                    Debug.Log($"{LOG_PREFIX} Missing striker pre-beat state snapshot. player={playerId}, beat={beatIndex}");
                    continue;
                }

                if (!strikerRegistry.Get(playerId).TryGetValue(out var striker)) {
                    continue;
                }

                striker.ApplyPreBeatStateDelta(snapshot);
            }
        }

        OnlineBeatNotificationSnapshot CreatePassNotification(int playerId, IMusicPlayer.BeatSignal signal) {
            return new OnlineBeatNotificationSnapshot(
                0,
                playerId,
                signal.BeatIndex,
                signal.BeatTime,
                OnlineBeatNotificationKind.Pass,
                BeatJudgeZone.Miss,
                default,
                Vector2.zero);
        }

        OnlineBeatNotificationSnapshot CreatePassNotification(int playerId, int beatIndex) {
            var beatTimeline = musicPlayer.CurrentBeatTimeline;
            var beatTime = beatIndex >= 0 && beatIndex < beatTimeline.Length
                ? beatTimeline[beatIndex]
                : musicPlayer.CurrentPlaybackTime;
            return new OnlineBeatNotificationSnapshot(
                0,
                playerId,
                beatIndex,
                beatTime,
                OnlineBeatNotificationKind.Pass,
                BeatJudgeZone.Miss,
                default,
                Vector2.zero);
        }

        void FillMissingRemoteBeatNotificationsIfNeeded(int playerId, int incomingBeatIndex) {
            if (incomingBeatIndex < 0) {
                return;
            }

            var expectedBeatIndex = lastReceivedOnlineBeatIndexByPlayer[playerId] + 1;
            if (incomingBeatIndex <= expectedBeatIndex) {
                return;
            }

            var filledCount = onlineCommandBuffer.FillMissingSubmissions(
                playerId,
                expectedBeatIndex,
                incomingBeatIndex,
                beatIndex => CreatePassNotification(playerId, beatIndex));
            if (filledCount > 0) {
                Debug.Log($"{LOG_PREFIX} Filled missing remote beats as pass. player={playerId}, start={expectedBeatIndex}, end={incomingBeatIndex}, count={filledCount}");
            }
        }

        void ExecuteOnlineNotification(int playerId, OnlineBeatNotificationSnapshot notification, IMusicPlayer.BeatSignal signal) {
            var player = beatPlayer[playerId];
            player.ClearSubmittedCommand(signal.BeatIndex);
            if (notification.Kind != OnlineBeatNotificationKind.Command) {
                player.ResetCombo();
                player.IncrementMiss();
                player.onBeatPassed.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, false,
                    BeatJudgeZone.Miss, notification.Button, notification.Direction, player.ComboCount.CurrentValue));
                return;
            }

            player.IncrementCombo();
            if (notification.Zone == BeatJudgeZone.Excellent) {
                player.IncrementExcellent();
            }
            else if (notification.Zone == BeatJudgeZone.Good) {
                player.IncrementGood();
            }

            player.AddScore(CalculateScore(notification.Zone));
            player.onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, true,
                notification.Zone, notification.Button, notification.Direction, player.ComboCount.CurrentValue));
        }

        bool IsOnlineBattle() {
            return appNetworkSetting.IsOnline.CurrentValue && battleOnlineSync.IsReady;
        }

        int ResolveLocalOnlinePlayerId() {
            return appNetworkSetting.LocalOnlinePlayerId;
        }

        int ResolveRemoteOnlinePlayerId() {
            return ResolveLocalOnlinePlayerId() == 0 ? 1 : 0;
        }

        void CleanupPreBeatStateTrackingBefore(int beatIndex) {
            publishedPreBeatStateSnapshotKeys.RemoveWhere(key => key.BeatIndex < beatIndex);
            appliedPreBeatStateSnapshotBeats.RemoveWhere(appliedBeatIndex => appliedBeatIndex < beatIndex);
        }

        void ResetOnlineCommandState() {
            onlineCommandBuffer.Clear();
            pendingOnlineBeatSignals.Clear();
            publishedPreBeatStateSnapshotKeys.Clear();
            appliedPreBeatStateSnapshotBeats.Clear();
            battleOnlineSync.ClearStrikerPreBeatStateSnapshotsBefore(int.MaxValue);
            lastOnlineBeatIndex = -1;
            for (var i = 0; i < lastReceivedOnlineBeatIndexByPlayer.Length; i++) {
                lastReceivedOnlineBeatIndexByPlayer[i] = -1;
            }
        }

        void ResetOnlineCommandStateForRoundResume() {
            var preserveFromBeatIndex = ResolveRoundResumePreserveBeatIndex();
            onlineCommandBuffer.ClearBeforeBeat(preserveFromBeatIndex);
            pendingOnlineBeatSignals.Clear();
            CleanupPreBeatStateTrackingBefore(preserveFromBeatIndex);
            battleOnlineSync.ClearStrikerPreBeatStateSnapshotsBefore(preserveFromBeatIndex);
            lastOnlineBeatIndex = preserveFromBeatIndex - 1;
            for (var i = 0; i < lastReceivedOnlineBeatIndexByPlayer.Length; i++) {
                if (i == ResolveLocalOnlinePlayerId()) {
                    continue;
                }
                lastReceivedOnlineBeatIndexByPlayer[i] = preserveFromBeatIndex - 1;
            }
            Debug.Log($"{LOG_PREFIX} Reset online command state for round resume. preserveFromBeat={preserveFromBeatIndex}");
        }

        int ResolveRoundResumePreserveBeatIndex() {
            var beatTimeline = musicPlayer.CurrentBeatTimeline;
            if (beatTimeline.Length == 0) {
                return 0;
            }

            var preserveFromTime = musicPlayer.CurrentPlaybackTime - Mathf.Max(0f, audioSetting.GoodWindow.CurrentValue);
            for (var i = 0; i < beatTimeline.Length; i++) {
                if (beatTimeline[i] >= preserveFromTime) {
                    return i;
                }
            }

            return beatTimeline.Length;
        }

        int CalculateScore(BeatJudgeZone zone) {
            var multiplier = zone == BeatJudgeZone.Excellent
                ? Mathf.Max(0f, audioSetting.ExcellentScoreMultiplier.CurrentValue)
                : Mathf.Max(0f, audioSetting.GoodScoreMultiplier.CurrentValue);
            return Mathf.RoundToInt(100f * multiplier);
        }

        public IReadOnlyDictionary<PlayerId, BeatPlayerBattleResult> GetBattleResults() {
            var results = new Dictionary<PlayerId, BeatPlayerBattleResult>(beatPlayer.Length);
            for (var playerId = 0; playerId < beatPlayer.Length; playerId++) {
                results[new PlayerId(playerId)] = new BeatPlayerBattleResult(beatPlayer[playerId].Score,
                    beatPlayer[playerId].Excellent, beatPlayer[playerId].Good, beatPlayer[playerId].Miss,
                    beatPlayer[playerId].MaxCombo);
            }

            return results;
        }

        public void ResetBattleState() {
            lastCommandPlaybackTime = -1f;
            ResetOnlineCommandState();
            for (var playerId = 0; playerId < beatPlayer.Length; playerId++) {
                beatPlayer[playerId].ResetBattleState();
            }
        }

        public void ResetRoundState() {
            lastCommandPlaybackTime = -1f;
            if (IsOnlineBattle()) {
                ResetOnlineCommandStateForRoundResume();
            } else {
                ResetOnlineCommandState();
            }
            for (var i = 0; i < beatPlayer.Length; i++) {
                beatPlayer[i].ResetForLoop();
            }
        }

        public void Pause() {
            isPaused = true;
        }

        public void Resume() {
            isPaused = false;
        }

        public IBeatPlayer GetBeatPlayer(int playerId) {
            if (playerId < 0 || playerId >= beatPlayer.Length) {
                throw new ArgumentOutOfRangeException(nameof(playerId),
                    $"Player ID must be between 0 and {beatPlayer.Length - 1}");
            }

            return beatPlayer[playerId];
        }

        public void Dispose() {

            foreach (var subscription in subscriptions) {
                subscription.Dispose();
            }

            subscriptions.Clear();
        }


    }
}
