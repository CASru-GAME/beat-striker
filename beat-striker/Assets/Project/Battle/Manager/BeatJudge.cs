
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
 
using App;
using R3;
using UnityEngine;

namespace Alice {

    public record BeatPlayerBattleResult(int Score, int Excellent, int Good, int Miss, int MaxCombo);

    public interface IBeatjudge {
        IBeatPlayer GetBeatPlayer(int playerId);
        IReadOnlyDictionary<PlayerId, BeatPlayerBattleResult> GetBattleResults();
        void ResetBattleState();
        void ResetRoundState();
        void Pause();
        void Resume();
    }

    public partial class BeatJudge : IBeatjudge, IDisposable {
        const string LOG_PREFIX = "[BeatJudge]";
        const int PLAYER_COUNT = 2;
        const float PRE_COMMAND_SNAPSHOT_INTERVAL_SECONDS = 0.05f;
        const float PRE_COMMAND_SNAPSHOT_WAIT_SECONDS = 0.05f;

        readonly IAudioSetting audioSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IBattleOnlineSync battleOnlineSync;
        readonly BeatOnlineCommandBuffer onlineCommandBuffer;
        readonly IStrikerRegistry strikerRegistry;
        readonly IMusicPlayer musicPlayer;
        readonly List<IDisposable> subscriptions = new();
        readonly Queue<IMusicPlayer.BeatSignal> pendingOnlineBeatSignals = new();
        readonly HashSet<int> activePreCommandSnapshotPublishBeats = new();
        readonly int[] lastReceivedOnlineBeatIndexByPlayer = new int[PLAYER_COUNT];
        BeatPlayer[] beatPlayer = new BeatPlayer[PLAYER_COUNT];
        float lastCommandPlaybackTime = -1f;
        int lastOnlineBeatIndex = -1;
        bool isOnlineBeatDrainRunning;
        bool isMusicPausedForRemoteOnlineBeat;
        float timeScaleBeforeRemoteOnlineBeatWait = 1f;
        bool isPaused;

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
                    if (isPaused || isMusicPausedForRemoteOnlineBeat) {
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

            subscriptions.Add(battleOnlineSync.OnBeatCommandReceived.Subscribe(ApplyOnlineBeatCommand));

            subscriptions.Add(musicPlayer.OnBeatTiming.Subscribe(signal => {
                if (isPaused) {
                    return;
                }

                if (IsOnlineBattle()) {
                    SubmitLocalOnlineMissIfNeeded(signal);
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

            var command = new OnlineBeatCommandSnapshot(
                0,
                playerId,
                result.BeatIndex,
                result.Time,
                battleOnlineSync.NetworkTime,
                result.IsSuccess,
                result.Zone,
                result.Button,
                result.Direction);
            if (onlineCommandBuffer.TrySubmit(command)) {
                battleOnlineSync.PublishBeatCommand(command);
                StartStrikerPreCommandSnapshotPublishLoop(command.BeatIndex);
            }
        }

        void ApplyOnlineBeatCommand(OnlineBeatCommandSnapshot command) {
            if (!IsOnlineBattle()
                || command.PlayerId == ResolveLocalOnlinePlayerId()
                || command.PlayerId < 0
                || command.PlayerId >= beatPlayer.Length) {
                return;
            }

            FillMissingRemoteBeatCommandsIfNeeded(command.PlayerId, command.BeatIndex);
            if (!onlineCommandBuffer.TrySubmit(command)) {
                return;
            }

            lastReceivedOnlineBeatIndexByPlayer[command.PlayerId] = Mathf.Max(
                lastReceivedOnlineBeatIndexByPlayer[command.PlayerId],
                command.BeatIndex);
            Debug.Log(
                $"{LOG_PREFIX} Applied remote online command. player={command.PlayerId}, beat={command.BeatIndex}, success={command.IsSuccess}, ready={onlineCommandBuffer.IsReady(command.BeatIndex, PLAYER_COUNT)}");
            var player = beatPlayer[command.PlayerId];
            if (command.IsSuccess) {
                player.onBeatCommandRequested.OnNext(new IBeatPlayer.BeatResult(
                    command.BeatIndex,
                    command.Time,
                    true,
                    command.Zone,
                    command.Button,
                    command.Direction,
                    player.ComboCount.CurrentValue));
            }
        }

        async Task ProcessOnlineBeatAsync(IMusicPlayer.BeatSignal signal) {
            if (lastOnlineBeatIndex >= 0 && signal.BeatIndex < lastOnlineBeatIndex) {
                ResetOnlineCommandState();
            }

            lastOnlineBeatIndex = signal.BeatIndex;

            if (isPaused || !IsOnlineBattle()) {
                return;
            }

            if (!onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)
                && !onlineCommandBuffer.HasSubmission(signal.BeatIndex, ResolveRemoteOnlinePlayerId())) {
                Debug.Log(
                    $"{LOG_PREFIX} Waiting online beat. beat={signal.BeatIndex}, localPlayer={ResolveLocalOnlinePlayerId()}, isHost={battleOnlineSync.IsSessionHost}");
                if (!await WaitForRemoteBeatCommandAsync(signal)) {
                    return;
                }
            }

            for (var playerId = 0; playerId < PLAYER_COUNT; playerId++) {
                if (!onlineCommandBuffer.HasSubmission(signal.BeatIndex, playerId)) {
                    onlineCommandBuffer.TrySubmit(CreateMissCommand(playerId, signal));
                }
            }

            await ApplyStrikerPreCommandSnapshotsAsync(signal.BeatIndex);

            for (var playerId = 0; playerId < PLAYER_COUNT; playerId++) {
                if (onlineCommandBuffer.TryGetCommand(signal.BeatIndex, playerId, out var command)) {
                    ExecuteOnlineCommand(playerId, command, signal);
                }
            }

            onlineCommandBuffer.CloseBeat(signal.BeatIndex);
            activePreCommandSnapshotPublishBeats.Remove(signal.BeatIndex);
            battleOnlineSync.ClearStrikerPreCommandSnapshotsBefore(signal.BeatIndex);
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

        void SubmitLocalOnlineMissIfNeeded(IMusicPlayer.BeatSignal signal) {
            var localPlayerId = ResolveLocalOnlinePlayerId();
            if (onlineCommandBuffer.HasSubmission(signal.BeatIndex, localPlayerId)) {
                return;
            }

            var command = CreateMissCommand(localPlayerId, signal);
            if (onlineCommandBuffer.TrySubmit(command)) {
                var player = beatPlayer[localPlayerId];
                if (player.HasAttempt(signal.BeatIndex)) {
                    player.onBeatCommandRequested.OnNext(new IBeatPlayer.BeatResult(
                        command.BeatIndex,
                        command.Time,
                        false,
                        BeatJudgeZone.Miss,
                        command.Button,
                        command.Direction,
                        player.ComboCount.CurrentValue));
                }
                Debug.Log(
                    $"{LOG_PREFIX} Submitted local online miss. player={localPlayerId}, beat={signal.BeatIndex}, ready={onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)}");
                battleOnlineSync.PublishBeatCommand(command);
                StartStrikerPreCommandSnapshotPublishLoop(command.BeatIndex);
            }
        }

        void StartStrikerPreCommandSnapshotPublishLoop(int beatIndex) {
            if (!IsOnlineBattle() || !activePreCommandSnapshotPublishBeats.Add(beatIndex)) {
                return;
            }

            _ = PublishStrikerPreCommandSnapshotsUntilBeatExecutesAsync(beatIndex);
        }

        async Task PublishStrikerPreCommandSnapshotsUntilBeatExecutesAsync(int beatIndex) {
            try {
                while (!isPaused && IsOnlineBattle() && activePreCommandSnapshotPublishBeats.Contains(beatIndex)) {
                    PublishLocalStrikerPreCommandSnapshot(beatIndex);
                    var nextPublishTime = battleOnlineSync.NetworkTime + PRE_COMMAND_SNAPSHOT_INTERVAL_SECONDS;
                    while (!isPaused
                           && IsOnlineBattle()
                           && activePreCommandSnapshotPublishBeats.Contains(beatIndex)
                           && battleOnlineSync.NetworkTime < nextPublishTime) {
                        await Task.Yield();
                    }
                }
            }
            finally {
                activePreCommandSnapshotPublishBeats.Remove(beatIndex);
            }
        }

        void PublishLocalStrikerPreCommandSnapshot(int beatIndex) {
            var localPlayerId = ResolveLocalOnlinePlayerId();
            if (!strikerRegistry.Get(localPlayerId).TryGetValue(out var striker)) {
                return;
            }

            var sentNetworkTime = battleOnlineSync.NetworkTime;
            battleOnlineSync.PublishStrikerPreCommandSnapshot(
                striker.BuildPreCommandSnapshot(beatIndex, sentNetworkTime));
        }

        async Task ApplyStrikerPreCommandSnapshotsAsync(int beatIndex) {
            var localPlayerId = ResolveLocalOnlinePlayerId();
            for (var playerId = 0; playerId < PLAYER_COUNT; playerId++) {
                if (playerId == localPlayerId) {
                    continue;
                }

                if (!battleOnlineSync.TryGetLatestStrikerPreCommandSnapshot(beatIndex, playerId, out var snapshot)) {
                    try {
                        snapshot = await battleOnlineSync.WaitForStrikerPreCommandSnapshotAsync(
                            beatIndex,
                            playerId,
                            PRE_COMMAND_SNAPSHOT_WAIT_SECONDS);
                    }
                    catch (TimeoutException) {
                        Debug.Log($"{LOG_PREFIX} Missing striker pre-command snapshot. player={playerId}, beat={beatIndex}");
                        continue;
                    }
                }

                if (!strikerRegistry.Get(playerId).TryGetValue(out var striker)) {
                    continue;
                }

                striker.ApplyPreCommandDelta(snapshot);
            }
        }

        OnlineBeatCommandSnapshot CreateMissCommand(int playerId, IMusicPlayer.BeatSignal signal) {
            return new OnlineBeatCommandSnapshot(
                0,
                playerId,
                signal.BeatIndex,
                signal.BeatTime,
                battleOnlineSync.NetworkTime,
                false,
                BeatJudgeZone.Miss,
                default,
                beatPlayer[playerId].CurrentInputDirection);
        }

        OnlineBeatCommandSnapshot CreateMissCommand(int playerId, int beatIndex) {
            var beatTimeline = musicPlayer.CurrentBeatTimeline;
            var beatTime = beatIndex >= 0 && beatIndex < beatTimeline.Length
                ? beatTimeline[beatIndex]
                : musicPlayer.CurrentPlaybackTime;
            return new OnlineBeatCommandSnapshot(
                0,
                playerId,
                beatIndex,
                beatTime,
                battleOnlineSync.NetworkTime,
                false,
                BeatJudgeZone.Miss,
                default,
                beatPlayer[playerId].CurrentInputDirection);
        }

        void FillMissingRemoteBeatCommandsIfNeeded(int playerId, int incomingBeatIndex) {
            if (incomingBeatIndex < 0) {
                return;
            }

            var expectedBeatIndex = lastReceivedOnlineBeatIndexByPlayer[playerId] + 1;
            if (incomingBeatIndex <= expectedBeatIndex) {
                return;
            }

            for (var beatIndex = expectedBeatIndex; beatIndex < incomingBeatIndex; beatIndex++) {
                if (onlineCommandBuffer.HasSubmission(beatIndex, playerId)) {
                    continue;
                }
                onlineCommandBuffer.TrySubmit(CreateMissCommand(playerId, beatIndex));
                Debug.Log($"{LOG_PREFIX} Filled missing remote beat as miss. player={playerId}, beat={beatIndex}");
            }
        }

        void ExecuteOnlineCommand(int playerId, OnlineBeatCommandSnapshot command, IMusicPlayer.BeatSignal signal) {
            var player = beatPlayer[playerId];
            player.ClearSubmittedCommand(signal.BeatIndex);
            if (!command.IsSuccess) {
                player.ResetCombo();
                player.IncrementMiss();
                player.onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, false,
                    BeatJudgeZone.Miss, command.Button, command.Direction, player.ComboCount.CurrentValue));
                // Keep direction state in sync with offline flow where pass updates direction each beat.
                player.onBeatPassed.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, false,
                    BeatJudgeZone.Miss, command.Button, command.Direction, player.ComboCount.CurrentValue));
                return;
            }

            player.IncrementCombo();
            if (command.Zone == BeatJudgeZone.Excellent) {
                player.IncrementExcellent();
            }
            else if (command.Zone == BeatJudgeZone.Good) {
                player.IncrementGood();
            }

            player.AddScore(CalculateScore(command.Zone));
            player.onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, true,
                command.Zone, command.Button, command.Direction, player.ComboCount.CurrentValue));
        }

        async Task<bool> WaitForRemoteBeatCommandAsync(IMusicPlayer.BeatSignal signal) {
            var remotePlayerId = ResolveRemoteOnlinePlayerId();
            if (onlineCommandBuffer.HasSubmission(signal.BeatIndex, remotePlayerId)) {
                return true;
            }

            var controlsMusic = battleOnlineSync.IsSessionHost;
            if (controlsMusic) {
                BeginLocalRemoteBeatWait(signal);
            }

            try {
                while (!isPaused
                       && IsOnlineBattle()
                       && !onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)) {
                    await Task.Yield();
                }

                if (isPaused || !IsOnlineBattle()) {
                    return false;
                }

                if (onlineCommandBuffer.TryGetCommand(signal.BeatIndex, remotePlayerId, out var remoteCommand)) {
                    musicPlayer.SyncPlaybackTime(EstimateRemotePlaybackTime(remoteCommand));
                }

                return true;
            }
            finally {
                if (controlsMusic) {
                    EndLocalRemoteBeatWait(!isPaused && IsOnlineBattle());
                }
            }
        }

        float EstimateRemotePlaybackTime(OnlineBeatCommandSnapshot remoteCommand) {
            var elapsedNetworkTime = Mathf.Max(0f, battleOnlineSync.NetworkTime - remoteCommand.SentNetworkTime);
            return Mathf.Max(0f, remoteCommand.Time + elapsedNetworkTime);
        }

        void CancelLocalOnlineDirection(IMusicPlayer.BeatSignal signal) {
            var localPlayerId = ResolveLocalOnlinePlayerId();
            beatPlayer[localPlayerId].onBeatPassed.OnNext(new IBeatPlayer.BeatResult(
                signal.BeatIndex,
                signal.BeatTime,
                false,
                BeatJudgeZone.Miss,
                default,
                Vector2.zero,
                beatPlayer[localPlayerId].ComboCount.CurrentValue));
        }

        void BeginLocalRemoteBeatWait(IMusicPlayer.BeatSignal signal) {
            if (isMusicPausedForRemoteOnlineBeat) {
                return;
            }

            isMusicPausedForRemoteOnlineBeat = true;
            timeScaleBeforeRemoteOnlineBeatWait = Time.timeScale;
            CancelLocalOnlineDirection(signal);
            musicPlayer.Pause();
            Time.timeScale = 0f;
        }

        void EndLocalRemoteBeatWait(bool resumeMusic) {
            if (!isMusicPausedForRemoteOnlineBeat) {
                return;
            }

            Time.timeScale = timeScaleBeforeRemoteOnlineBeatWait;
            if (resumeMusic) {
                musicPlayer.Resume();
            }
            isMusicPausedForRemoteOnlineBeat = false;
        }

        bool IsOnlineBattle() {
            return appNetworkSetting.IsOnline.CurrentValue && battleOnlineSync.IsReady;
        }

        int ResolveLocalOnlinePlayerId() {
            return battleOnlineSync.IsSessionHost ? 0 : 1;
        }

        int ResolveRemoteOnlinePlayerId() {
            return ResolveLocalOnlinePlayerId() == 0 ? 1 : 0;
        }

        void ResetOnlineCommandState() {
            onlineCommandBuffer.Clear();
            pendingOnlineBeatSignals.Clear();
            activePreCommandSnapshotPublishBeats.Clear();
            EndLocalRemoteBeatWait(false);
            battleOnlineSync.ClearStrikerPreCommandSnapshotsBefore(int.MaxValue);
            lastOnlineBeatIndex = -1;
            for (var i = 0; i < lastReceivedOnlineBeatIndexByPlayer.Length; i++) {
                lastReceivedOnlineBeatIndexByPlayer[i] = -1;
            }
        }

        void ResetOnlineCommandStateForRoundResume() {
            var preserveFromBeatIndex = ResolveRoundResumePreserveBeatIndex();
            onlineCommandBuffer.ClearBeforeBeat(preserveFromBeatIndex);
            pendingOnlineBeatSignals.Clear();
            activePreCommandSnapshotPublishBeats.Clear();
            EndLocalRemoteBeatWait(false);
            battleOnlineSync.ClearStrikerPreCommandSnapshotsBefore(preserveFromBeatIndex);
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