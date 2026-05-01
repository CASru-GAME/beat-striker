
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

        readonly IAudioSetting audioSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IBattleOnlineSync battleOnlineSync;
        readonly BeatOnlineCommandBuffer onlineCommandBuffer;
        readonly List<IDisposable> subscriptions = new();
        readonly Queue<IMusicPlayer.BeatSignal> pendingOnlineBeatSignals = new();
        BeatPlayer[] beatPlayer = new BeatPlayer[PLAYER_COUNT];
        float lastCommandPlaybackTime = -1f;
        int lastOnlineBeatIndex = -1;
        bool isOnlineBeatDrainRunning;
        bool isPaused;

        public BeatJudge(IGamePadRegistry gamePadRegistry, IMusicPlayer musicPlayer, IAudioSetting audioSetting,
            IAppNetworkSetting appNetworkSetting, IBattleOnlineSync battleOnlineSync,
            BeatOnlineCommandBuffer onlineCommandBuffer) {
            this.audioSetting = audioSetting;
            this.appNetworkSetting = appNetworkSetting;
            this.battleOnlineSync = battleOnlineSync;
            this.onlineCommandBuffer = onlineCommandBuffer;


            for (int i = 0; i < beatPlayer.Length; i++) {
                beatPlayer[i] = new BeatPlayer();
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
                    if (isPaused) {
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
                    var isGood = isTimingSuccess && player.TrySavePendingCommand(result.BeatIndex, result.Zone, button,
                        player.CurrentInputDirection);
                    if (isTimingSuccess && !isGood) {
                    }

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
                result.IsSuccess,
                result.Zone,
                result.Button,
                result.Direction);
            if (onlineCommandBuffer.TrySubmit(command)) {
                battleOnlineSync.PublishBeatCommand(command);
            }
        }

        void ApplyOnlineBeatCommand(OnlineBeatCommandSnapshot command) {
            if (!IsOnlineBattle()
                || command.PlayerId == ResolveLocalOnlinePlayerId()
                || command.PlayerId < 0
                || command.PlayerId >= beatPlayer.Length) {
                return;
            }

            if (!onlineCommandBuffer.TrySubmit(command)) {
                return;
            }

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

            SubmitLocalOnlineMissIfNeeded(signal);
            if (isPaused || !IsOnlineBattle()) {
                return;
            }

            if (!onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)) {
                Debug.Log(
                    $"{LOG_PREFIX} Waiting online beat. beat={signal.BeatIndex}, localPlayer={ResolveLocalOnlinePlayerId()}, isHost={battleOnlineSync.IsSessionHost}");
                while (!isPaused
                       && IsOnlineBattle()
                       && !onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)) {
                    await Task.Yield();
                }

                if (isPaused || !IsOnlineBattle()) {
                    return;
                }
            }

            for (var playerId = 0; playerId < PLAYER_COUNT; playerId++) {
                if (!onlineCommandBuffer.HasSubmission(signal.BeatIndex, playerId)) {
                    onlineCommandBuffer.TrySubmit(CreateMissCommand(playerId, signal));
                }
            }

            for (var playerId = 0; playerId < PLAYER_COUNT; playerId++) {
                if (onlineCommandBuffer.TryGetCommand(signal.BeatIndex, playerId, out var command)) {
                    ExecuteOnlineCommand(playerId, command, signal);
                }
            }

            onlineCommandBuffer.CloseBeat(signal.BeatIndex);
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
                Debug.Log(
                    $"{LOG_PREFIX} Submitted local online miss. player={localPlayerId}, beat={signal.BeatIndex}, ready={onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)}");
                battleOnlineSync.PublishBeatCommand(command);
            }
        }

        OnlineBeatCommandSnapshot CreateMissCommand(int playerId, IMusicPlayer.BeatSignal signal) {
            return new OnlineBeatCommandSnapshot(
                0,
                playerId,
                signal.BeatIndex,
                signal.BeatTime,
                false,
                BeatJudgeZone.Miss,
                default,
                beatPlayer[playerId].CurrentInputDirection);
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

        bool IsOnlineBattle() {
            return appNetworkSetting.IsOnline.CurrentValue && battleOnlineSync.IsReady;
        }

        int ResolveLocalOnlinePlayerId() {
            return battleOnlineSync.IsSessionHost ? 0 : 1;
        }

        void ResetOnlineCommandState() {
            onlineCommandBuffer.Clear();
            pendingOnlineBeatSignals.Clear();
            lastOnlineBeatIndex = -1;
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
            ResetOnlineCommandState();
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