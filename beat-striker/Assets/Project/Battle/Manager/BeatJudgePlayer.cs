
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
 
using App;
using R3;
using UnityEngine;

namespace Alice {

    public interface IBeatPlayer {
        public Observable<BeatResult> OnBeatCommandRequested { get; }
        public Observable<BeatResult> OnBeatCommandExecuted { get; }
        public Observable<BeatResult> OnBeatPassed { get; }
        public ReadOnlyReactiveProperty<int> ComboCount { get; }

        public record BeatResult(
            int BeatIndex,
            float Time,
            bool IsSuccess,
            BeatJudgeZone Zone,
            GamePadButton Button,
            Vector2 Direction,
            int ComboCount);
    }

    public partial class BeatJudge {
        
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