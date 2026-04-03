
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

        public record BeatResult(float Time, bool IsSuccess, GamePadButton Button, Vector2 Direction, int ComboCount);
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

                subscriptions.Add(gamePad.OnDirection.Subscribe(direction => {
                    beatPlayer[playerIndex].UpdateInputDirection(direction);
                }));

                subscriptions.Add(gamePad.OnDirectionCanceled.Subscribe(_ => {
                    beatPlayer[playerIndex].ClearInputDirection();
                }));

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
                    var isGood = isTimingGood && player.TrySavePendingCommand(result.BeatIndex, button, player.CurrentInputDirection);
                    if (isTimingGood && !isGood) {
                    }
                    player.onBeatCommandRequested.OnNext(new IBeatPlayer.BeatResult(time, isGood, button, player.CurrentInputDirection, player.ComboCount.CurrentValue));
                    // Record that player attempted this beat so it's not considered a pass later
                    player.RecordAttempt(result.BeatIndex);
                });
                subscriptions.Add(subscription);
            }

            subscriptions.Add(musicPlayer.OnBeatTiming.Subscribe(signal => {
                
                for (var playerIndex = 0; playerIndex < beatPlayer.Length; playerIndex++) {
                    if (!beatPlayer[playerIndex].TryConsumePendingCommand(signal.BeatIndex, out var button, out var direction)) {
                        // If player attempted this beat (but it wasn't saved as pending), it's a miss rather than a pass
                        if (beatPlayer[playerIndex].HasAttempt(signal.BeatIndex)) {
                            beatPlayer[playerIndex].ClearAttempt(signal.BeatIndex);
                            beatPlayer[playerIndex].ResetCombo();
                            beatPlayer[playerIndex].onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatTime, false, default, Vector2.zero, beatPlayer[playerIndex].ComboCount.CurrentValue));
                            continue;
                        }

                        // No pending command and no attempt -> player passed the beat
                        beatPlayer[playerIndex].ResetCombo();
                        beatPlayer[playerIndex].onBeatPassed.OnNext(new IBeatPlayer.BeatResult(signal.BeatTime, false, default, beatPlayer[playerIndex].CurrentInputDirection, beatPlayer[playerIndex].ComboCount.CurrentValue));
                        continue;
                    }

                    beatPlayer[playerIndex].IncrementCombo();
                    beatPlayer[playerIndex].onBeatCommandExecuted.OnNext(new IBeatPlayer.BeatResult(signal.BeatTime, true, button, direction, beatPlayer[playerIndex].ComboCount.CurrentValue));
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
            readonly Dictionary<int, PendingCommand> pendingCommands = new Dictionary<int, PendingCommand>();
            readonly HashSet<int> attemptedCommands = new HashSet<int>();
            readonly ReactiveProperty<int> comboCount = new(0);
            public Subject<IBeatPlayer.BeatResult> onBeatCommandRequested = new Subject<IBeatPlayer.BeatResult>();
            public Subject<IBeatPlayer.BeatResult> onBeatCommandExecuted = new Subject<IBeatPlayer.BeatResult>();
            public Subject<IBeatPlayer.BeatResult> onBeatPassed = new Subject<IBeatPlayer.BeatResult>();
            Vector2 currentInputDirection = Vector2.zero;

            record PendingCommand(GamePadButton Button, Vector2 Direction);

            public BeatPlayer(int playerIndex) {
                this.playerIndex = playerIndex;
            }

            public Observable<IBeatPlayer.BeatResult> OnBeatCommandRequested => onBeatCommandRequested;
            public Observable<IBeatPlayer.BeatResult> OnBeatCommandExecuted => onBeatCommandExecuted;
            public Observable<IBeatPlayer.BeatResult> OnBeatPassed => onBeatPassed;
            public ReadOnlyReactiveProperty<int> ComboCount => comboCount;
            public Vector2 CurrentInputDirection => currentInputDirection;

            public bool TrySavePendingCommand(int beatIndex, GamePadButton button, Vector2 direction) {
                if (pendingCommands.ContainsKey(beatIndex)) {
                    
                    return false;
                }

                pendingCommands[beatIndex] = new PendingCommand(button, direction);
                
                return true;
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

            public bool TryConsumePendingCommand(int beatIndex, out GamePadButton button, out Vector2 direction) {
                if (!pendingCommands.TryGetValue(beatIndex, out var command)) {
                    button = default;
                    direction = Vector2.zero;
                    
                    return false;
                }

                pendingCommands.Remove(beatIndex);
                attemptedCommands.Remove(beatIndex);
                button = command.Button;
                direction = command.Direction;
                
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