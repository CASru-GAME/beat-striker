
using System;
using System.Collections.Generic;
 
using R3;
using UnityEngine;

namespace Alice {

    public interface IBeatPlayer{
        public Observable<BeatResult> OnBeatCommandRequested { get; }
        public Observable<BeatResult> OnBeatCommandExecuted { get; }
        public Observable<BeatResult> OnBeatPassed { get; }
        public ReadOnlyReactiveProperty<int> ComboCount { get; }

        public record BeatResult(float Time, bool IsSuccess, GamePadButton Button, int ComboCount);
    }

    public interface IBeatjudge {
        IBeatPlayer GetBeatPlayer(int playerId);
        void ResetRoundState();
    }

    public class BeatJudge : IBeatjudge, IDisposable {
        readonly IMusicPlayer musicPlayer;
        readonly List<IDisposable> subscriptions = new();
        BeatPlayer[] beatPlayer = new BeatPlayer[2];
        float lastCommandPlaybackTime = -1f;

        public BeatJudge(IGamePadRegistry gamePadRegistry, IMusicPlayer musicPlayer) {
            this.musicPlayer = musicPlayer;
            

            for(int i = 0; i < beatPlayer.Length; i++) {
                beatPlayer[i] = new BeatPlayer(i);
            }

            for(int i = 0; i < beatPlayer.Length; i++) {
                var playerIndex = i;
                var gamePad = gamePadRegistry.Get(playerIndex);
                var subscription = gamePad.OnButtonDown.Subscribe(button => {
                    if (musicPlayer.CurrentBeatTimeline.Length == 0) {
                        return;
                    }

                    var player = beatPlayer[playerIndex];
                    var time = musicPlayer.CurrentPlaybackTime;
                    if (lastCommandPlaybackTime >= 0f && time < lastCommandPlaybackTime) {
                        for (var j = 0; j < beatPlayer.Length; j++) {
                            beatPlayer[j].ResetForLoop();
                        }
                    }
                    lastCommandPlaybackTime = time;

                    var result = musicPlayer.JudgeTiming(time);
                    var isTimingGood = result.Zone == BeatJudgeZone.Good && time < result.BeatTime;
                    
                    var isGood = isTimingGood && player.TrySavePendingCommand(result.BeatIndex, button);
                    if (isTimingGood && !isGood) {
                    }
                    player.onBeatCommandRequested.OnNext(new IBeatPlayer.BeatResult(time, isGood, button, player.ComboCount.CurrentValue));
                    // Record that player attempted this beat so it's not considered a pass later
                    player.RecordAttempt(result.BeatIndex);
                });
                subscriptions.Add(subscription);
            }

            subscriptions.Add(musicPlayer.OnBeatTiming.Subscribe(signal => {
                
                for (var playerIndex = 0; playerIndex < beatPlayer.Length; playerIndex++) {
                    if (!beatPlayer[playerIndex].TryConsumePendingCommand(signal.BeatIndex, out var button)) {
                        // If player attempted this beat (but it wasn't saved as pending), it's a miss rather than a pass
                        if (beatPlayer[playerIndex].HasAttempt(signal.BeatIndex)) {
                            beatPlayer[playerIndex].ClearAttempt(signal.BeatIndex);
                            beatPlayer[playerIndex].ResetCombo();
                            beatPlayer[playerIndex].onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatTime, false, default, beatPlayer[playerIndex].ComboCount.CurrentValue));
                            continue;
                        }

                        // No pending command and no attempt -> player passed the beat
                        beatPlayer[playerIndex].ResetCombo();
                        beatPlayer[playerIndex].onBeatPassed.OnNext(new IBeatPlayer.BeatResult(signal.BeatTime, false, default, beatPlayer[playerIndex].ComboCount.CurrentValue));
                        continue;
                    }

                    beatPlayer[playerIndex].IncrementCombo();
                    beatPlayer[playerIndex].onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatTime, true, button, beatPlayer[playerIndex].ComboCount.CurrentValue));
                }
            }));
        }

        public void ResetRoundState() {
            lastCommandPlaybackTime = -1f;
            for (var i = 0; i < beatPlayer.Length; i++) {
                beatPlayer[i].ResetForLoop();
            }
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
            readonly int playerIndex;
            readonly Dictionary<int, GamePadButton> pendingCommands = new Dictionary<int, GamePadButton>();
            readonly HashSet<int> attemptedCommands = new HashSet<int>();
            readonly ReactiveProperty<int> comboCount = new(0);
            public Subject<IBeatPlayer.BeatResult> onBeatCommandRequested = new Subject<IBeatPlayer.BeatResult>();
            public Subject<IBeatPlayer.BeatResult> onBeatCommandExecuted = new Subject<IBeatPlayer.BeatResult>();
            public Subject<IBeatPlayer.BeatResult> onBeatPassed = new Subject<IBeatPlayer.BeatResult>();

            public BeatPlayer(int playerIndex) {
                this.playerIndex = playerIndex;
            }

            public Observable<IBeatPlayer.BeatResult> OnBeatCommandRequested => onBeatCommandRequested;
            public Observable<IBeatPlayer.BeatResult> OnBeatCommandExecuted => onBeatCommandExecuted;
            public Observable<IBeatPlayer.BeatResult> OnBeatPassed => onBeatPassed;
            public ReadOnlyReactiveProperty<int> ComboCount => comboCount;

            public bool TrySavePendingCommand(int beatIndex, GamePadButton button) {
                if (pendingCommands.ContainsKey(beatIndex)) {
                    
                    return false;
                }

                pendingCommands[beatIndex] = button;
                
                return true;
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

            public bool TryConsumePendingCommand(int beatIndex, out GamePadButton button) {
                if (!pendingCommands.TryGetValue(beatIndex, out button)) {
                    
                    return false;
                }

                pendingCommands.Remove(beatIndex);
                attemptedCommands.Remove(beatIndex);
                
                return true;
            }

            public void ResetForLoop() {
                pendingCommands.Clear();
                attemptedCommands.Clear();
                comboCount.OnNext(0);
            }

            public void IncrementCombo() {
                comboCount.OnNext(comboCount.CurrentValue + 1);
            }

            public void ResetCombo() {
                comboCount.OnNext(0);
            }

            
        }
    }
}