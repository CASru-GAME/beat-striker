
using System;
using System.Collections.Generic;
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
            int ComboCount,
            int? ForfeitedBeatIndex = null);
    }

    public partial class BeatJudge {
        
        class BeatPlayer : IBeatPlayer {
            const double CONSECUTIVE_MISS_SECONDS = 0.2;

            readonly HashSet<int> successfulBeats = new();
            readonly HashSet<int> forfeitedBeats = new();
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
            double lastMissTime = double.NegativeInfinity;
            int pendingBeatIndex = -1;

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
            public bool IsInputLocked => pendingBeatIndex >= 0;
            public bool HasSucceeded(int beatIndex) => successfulBeats.Contains(beatIndex);
            public bool IsBeatAvailable(int beatIndex) => !HasSucceeded(beatIndex) && !IsBeatForfeited(beatIndex);
            public void MarkSucceeded(int beatIndex) => successfulBeats.Add(beatIndex);

            public bool IsBeatForfeited(int beatIndex) => forfeitedBeats.Contains(beatIndex);

            public int? RegisterMiss(int beatIndex, double time) {
                if (beatIndex >= 0 && time - lastMissTime <= CONSECUTIVE_MISS_SECONDS) {
                    forfeitedBeats.Add(beatIndex);
                    ResetMissStreak();
                    return beatIndex;
                }

                lastMissTime = time;
                return null;
            }

            public void ResetMissStreak() => lastMissTime = double.NegativeInfinity;

            public void ResetMissPenalty() {
                forfeitedBeats.Clear();
                ResetMissStreak();
            }

            public void ReserveOnlineBeat(int beatIndex) => pendingBeatIndex = beatIndex;

            public void UpdateInputDirection(Vector2 direction) {
                currentInputDirection = direction;
            }

            public void ClearInputDirection() {
                currentInputDirection = Vector2.zero;
            }

            public void ClearSubmittedCommand(int beatIndex) {
                if (pendingBeatIndex == beatIndex) pendingBeatIndex = -1;
            }

            public void CancelPendingCommands() => pendingBeatIndex = -1;

            public void ResetForLoop() {
                CancelPendingCommands();
                successfulBeats.Clear();
                ResetMissPenalty();
                comboCount.OnNext(0);
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
