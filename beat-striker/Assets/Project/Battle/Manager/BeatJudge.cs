
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
 
using App;
using R3;
using UnityEngine;

namespace Alice {

    public record BeatPlayerBattleResult(int Score, int Excellent, int Good, int Miss, int MaxCombo);

    public interface IBeatPlayer{
        public Observable<BeatResult> OnBeatCommandRequested { get; }
        public Observable<BeatResult> OnBeatCommandExecuted { get; }
        public Observable<BeatResult> OnBeatPassed { get; }
        public ReadOnlyReactiveProperty<int> ComboCount { get; }

        public record BeatResult(int BeatIndex, float Time, bool IsSuccess, BeatJudgeZone Zone, GamePadButton Button, Vector2 Direction, int ComboCount);
    }

    public interface IBeatjudge {
        IBeatPlayer GetBeatPlayer(int playerId);
        IReadOnlyDictionary<PlayerId, BeatPlayerBattleResult> GetBattleResults();
        void ResetBattleState();
        void ResetRoundState();
        void Pause();
        void Resume();
    }

    public class BeatJudge : IBeatjudge, IDisposable {
        const int PLAYER_COUNT = 2;
        const float ONLINE_BEAT_RESUME_LEAD_SECONDS = 0.15f;

        readonly IMusicPlayer musicPlayer;
        readonly IAudioSetting audioSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IBattleOnlineSync battleOnlineSync;
        readonly BeatOnlineCommandBuffer onlineCommandBuffer;
        readonly List<IDisposable> subscriptions = new();
        BeatPlayer[] beatPlayer = new BeatPlayer[PLAYER_COUNT];
        float lastCommandPlaybackTime = -1f;
        int lastOnlineBeatIndex = -1;
        int onlineCommandGeneration;
        bool isPaused;

        public BeatJudge(IGamePadRegistry gamePadRegistry, IMusicPlayer musicPlayer, IAudioSetting audioSetting, IAppNetworkSetting appNetworkSetting, IBattleOnlineSync battleOnlineSync, BeatOnlineCommandBuffer onlineCommandBuffer) {
            this.musicPlayer = musicPlayer;
            this.audioSetting = audioSetting;
            this.appNetworkSetting = appNetworkSetting;
            this.battleOnlineSync = battleOnlineSync;
            this.onlineCommandBuffer = onlineCommandBuffer;
            

            for(int i = 0; i < beatPlayer.Length; i++) {
                beatPlayer[i] = new BeatPlayer();
            }

            for(int i = 0; i < beatPlayer.Length; i++) {
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
                    var isGood = isTimingSuccess && player.TrySavePendingCommand(result.BeatIndex, result.Zone, button, player.CurrentInputDirection);
                    if (isTimingSuccess && !isGood) {
                    }
                    if (isGood) {
                        player.LockInputUntilBeat(result.BeatIndex);
                    }
                    var requestZone = isGood ? result.Zone : BeatJudgeZone.Miss;
                    var beatResult = new IBeatPlayer.BeatResult(result.BeatIndex, time, isGood, requestZone, button, player.CurrentInputDirection, player.ComboCount.CurrentValue);
                    player.onBeatCommandRequested.OnNext(beatResult);
                    SubmitLocalOnlineCommandIfNeeded(playerIndex, beatResult);
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
                    _ = ProcessOnlineBeatAsync(signal);
                    return;
                }

                for (var playerIndex = 0; playerIndex < beatPlayer.Length; playerIndex++) {
                    if (!beatPlayer[playerIndex].TryConsumePendingCommand(signal.BeatIndex, out var zone, out var button, out var direction)) {
                        // If player attempted this beat (but it wasn't saved as pending), it's a miss rather than a pass
                        if (beatPlayer[playerIndex].HasAttempt(signal.BeatIndex)) {
                            beatPlayer[playerIndex].ClearAttempt(signal.BeatIndex);
                            beatPlayer[playerIndex].ResetCombo();
                            beatPlayer[playerIndex].IncrementMiss();
                            beatPlayer[playerIndex].onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, false, BeatJudgeZone.Miss, default, Vector2.zero, beatPlayer[playerIndex].ComboCount.CurrentValue));
                            continue;
                        }

                        // No pending command and no attempt -> player passed the beat
                        beatPlayer[playerIndex].ResetCombo();
                        beatPlayer[playerIndex].IncrementMiss();
                        beatPlayer[playerIndex].onBeatPassed.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, false, BeatJudgeZone.Miss, default, beatPlayer[playerIndex].CurrentInputDirection, beatPlayer[playerIndex].ComboCount.CurrentValue));
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
                    beatPlayer[playerIndex].onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, true, zone, button, direction, beatPlayer[playerIndex].ComboCount.CurrentValue));
                }
            }));
        }

        void SubmitLocalOnlineCommandIfNeeded(int playerId, IBeatPlayer.BeatResult result) {
            if (!IsOnlineBattle() || playerId != appNetworkSetting.LocalOnlinePlayerId || result.BeatIndex < 0) {
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
                || command.PlayerId == appNetworkSetting.LocalOnlinePlayerId
                || command.PlayerId < 0
                || command.PlayerId >= beatPlayer.Length) {
                return;
            }

            if (!onlineCommandBuffer.TrySubmit(command)) {
                return;
            }

            var player = beatPlayer[command.PlayerId];
            player.onBeatCommandRequested.OnNext(new IBeatPlayer.BeatResult(
                command.BeatIndex,
                command.Time,
                command.IsSuccess,
                command.Zone,
                command.Button,
                command.Direction,
                player.ComboCount.CurrentValue));
        }

        async Task ProcessOnlineBeatAsync(IMusicPlayer.BeatSignal signal) {
            if (lastOnlineBeatIndex >= 0 && signal.BeatIndex < lastOnlineBeatIndex) {
                ResetOnlineCommandState();
            }
            lastOnlineBeatIndex = signal.BeatIndex;
            var generation = onlineCommandGeneration;

            SubmitLocalOnlineMissIfNeeded(signal);
            musicPlayer.Pause();

            OnlineBeatSyncResumeSnapshot resumeSnapshot;
            if (battleOnlineSync.IsSessionHost) {
                while (!isPaused
                    && IsOnlineBattle()
                    && !onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)) {
                    await Task.Yield();
                }

                if (isPaused || !IsOnlineBattle() || generation != onlineCommandGeneration) {
                    return;
                }

                var resumeNetworkTime = battleOnlineSync.NetworkTime + ONLINE_BEAT_RESUME_LEAD_SECONDS;
                battleOnlineSync.PublishBeatSyncResume(signal.BeatIndex, resumeNetworkTime);
                resumeSnapshot = new OnlineBeatSyncResumeSnapshot(0, signal.BeatIndex, resumeNetworkTime);
            }
            else {
                resumeSnapshot = await battleOnlineSync.WaitForBeatSyncResumeAsync(signal.BeatIndex);
            }

            if (isPaused || !IsOnlineBattle() || generation != onlineCommandGeneration) {
                return;
            }

            while (!isPaused
                && IsOnlineBattle()
                && !onlineCommandBuffer.IsReady(signal.BeatIndex, PLAYER_COUNT)) {
                await Task.Yield();
            }

            await WaitForNetworkTimeAsync(resumeSnapshot.ResumeNetworkTime);

            if (isPaused || !IsOnlineBattle() || generation != onlineCommandGeneration) {
                return;
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
            musicPlayer.Resume();
        }

        void SubmitLocalOnlineMissIfNeeded(IMusicPlayer.BeatSignal signal) {
            var localPlayerId = appNetworkSetting.LocalOnlinePlayerId;
            if (onlineCommandBuffer.HasSubmission(signal.BeatIndex, localPlayerId)) {
                return;
            }

            var command = CreateMissCommand(localPlayerId, signal);
            if (onlineCommandBuffer.TrySubmit(command)) {
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
                player.onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, false, BeatJudgeZone.Miss, command.Button, command.Direction, player.ComboCount.CurrentValue));
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
            player.onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatIndex, signal.BeatTime, true, command.Zone, command.Button, command.Direction, player.ComboCount.CurrentValue));
        }

        bool IsOnlineBattle() {
            return appNetworkSetting.IsOnline.CurrentValue && battleOnlineSync.IsReady;
        }

        async Task WaitForNetworkTimeAsync(float networkTime) {
            while (!isPaused && IsOnlineBattle() && battleOnlineSync.NetworkTime < networkTime) {
                await Task.Yield();
            }
        }

        void ResetOnlineCommandState() {
            onlineCommandBuffer.Clear();
            lastOnlineBeatIndex = -1;
            onlineCommandGeneration += 1;
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
                results[new PlayerId(playerId)] = new BeatPlayerBattleResult(beatPlayer[playerId].Score, beatPlayer[playerId].Excellent, beatPlayer[playerId].Good, beatPlayer[playerId].Miss, beatPlayer[playerId].MaxCombo);
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
                throw new ArgumentOutOfRangeException(nameof(playerId), $"Player ID must be between 0 and {beatPlayer.Length - 1}");
            }
            return beatPlayer[playerId];
        }

        public void Dispose() {
            
            foreach (var subscription in subscriptions) {
                subscription.Dispose();
            }
            subscriptions.Clear();
        }

        

        class BeatPlayer : IBeatPlayer {
            readonly Dictionary<int, PendingCommand> pendingCommands = new Dictionary<int, PendingCommand>();
            readonly HashSet<int> attemptedCommands = new HashSet<int>();
            readonly ReactiveProperty<int> comboCount = new(0);
            public Subject<IBeatPlayer.BeatResult> onBeatCommandRequested = new Subject<IBeatPlayer.BeatResult>();
            public Subject<IBeatPlayer.BeatResult> onBeatCommandExecuted = new Subject<IBeatPlayer.BeatResult>();
            public Subject<IBeatPlayer.BeatResult> onBeatPassed = new Subject<IBeatPlayer.BeatResult>();
            Vector2 currentInputDirection = Vector2.zero;
            int score;
            int excellent;
            int good;
            int miss;
            int maxCombo;
            bool isInputLocked;
            int lockedBeatIndex = -1;

            record PendingCommand(BeatJudgeZone Zone, GamePadButton Button, Vector2 Direction);

            public Observable<IBeatPlayer.BeatResult> OnBeatCommandRequested => onBeatCommandRequested;
            public Observable<IBeatPlayer.BeatResult> OnBeatCommandExecuted => onBeatCommandExecuted;
            public Observable<IBeatPlayer.BeatResult> OnBeatPassed => onBeatPassed;
            public ReadOnlyReactiveProperty<int> ComboCount => comboCount;
            public Vector2 CurrentInputDirection => currentInputDirection;
            public int Score => score;
            public int Excellent => excellent;
            public int Good => good;
            public int Miss => miss;
            public int MaxCombo => maxCombo;
            public bool IsInputLocked => isInputLocked;

            public bool TrySavePendingCommand(int beatIndex, BeatJudgeZone zone, GamePadButton button, Vector2 direction) {
                if (pendingCommands.ContainsKey(beatIndex)) {
                    
                    return false;
                }

                pendingCommands[beatIndex] = new PendingCommand(zone, button, direction);
                
                return true;
            }

            public void LockInputUntilBeat(int beatIndex) {
                isInputLocked = true;
                lockedBeatIndex = beatIndex;
            }

            public void UnlockInputIfBeatMatched(int beatIndex) {
                if (!isInputLocked || lockedBeatIndex != beatIndex) {
                    return;
                }

                isInputLocked = false;
                lockedBeatIndex = -1;
            }

            public void UpdateInputDirection(Vector2 direction) {
                currentInputDirection = direction;
            }

            public void ClearInputDirection() {
                currentInputDirection = Vector2.zero;
            }

            public void RecordAttempt(int beatIndex) {
                if (beatIndex < 0) return;
                attemptedCommands.Add(beatIndex);
            }

            public bool HasAttempt(int beatIndex) {
                return attemptedCommands.Contains(beatIndex);
            }

            public void ClearAttempt(int beatIndex) {
                attemptedCommands.Remove(beatIndex);
            }

            public bool TryConsumePendingCommand(int beatIndex, out BeatJudgeZone zone, out GamePadButton button, out Vector2 direction) {
                if (!pendingCommands.TryGetValue(beatIndex, out var command)) {
                    zone = BeatJudgeZone.Miss;
                    button = default;
                    direction = Vector2.zero;
                    
                    return false;
                }

                pendingCommands.Remove(beatIndex);
                attemptedCommands.Remove(beatIndex);
                zone = command.Zone;
                button = command.Button;
                direction = command.Direction;
                
                return true;
            }

            public void ClearSubmittedCommand(int beatIndex) {
                pendingCommands.Remove(beatIndex);
                attemptedCommands.Remove(beatIndex);
                UnlockInputIfBeatMatched(beatIndex);
            }

            public void ResetForLoop() {
                pendingCommands.Clear();
                attemptedCommands.Clear();
                comboCount.OnNext(0);
                isInputLocked = false;
                lockedBeatIndex = -1;
            }

            public void ResetBattleState() {
                score = 0;
                excellent = 0;
                good = 0;
                miss = 0;
                maxCombo = 0;
                ResetForLoop();
            }

            public void AddScore(int value) {
                score += value;
            }

            public void IncrementExcellent() {
                excellent += 1;
            }

            public void IncrementGood() {
                good += 1;
            }

            public void IncrementMiss() {
                miss += 1;
            }

            public void IncrementCombo() {
                var nextCombo = comboCount.CurrentValue + 1;
                comboCount.OnNext(nextCombo);
                if (maxCombo < nextCombo) {
                    maxCombo = nextCombo;
                }
            }

            public void ResetCombo() {
                comboCount.OnNext(0);
            }

            
        }
    }
}