using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using CorePlayerId = App.PlayerId;

namespace Alice {
    public sealed class BattleFlowMusicEndHandler {
        const string LOG_PREFIX = "[BattleFlow]";
        readonly BattleFlowStateMachine stateMachine;
        readonly IAISetting aiSetting;
        readonly IBattleJudge battleJudge;
        readonly IBeatjudge beatJudge;
        readonly IBattleOnlineSync battleOnlineSync;
        readonly BattleFlowPauseHandler pauseHandler;
        readonly Func<bool, bool> beginRoundResolution;
        readonly Func<int> getCurrentRound;
        readonly Func<CorePlayerId, IReadOnlyDictionary<CorePlayerId, int>, bool, Task> completeBattleWithWinnerAsync;
        readonly Func<int> resolveTopHitPointPlayerId;
        readonly Action<int, int, int, bool, int, bool, IReadOnlyDictionary<CorePlayerId, int>> publishRoundOutcome;
        bool musicEndBattleRequested;

        public bool IsMusicEndBattleRequested => musicEndBattleRequested;
        public bool ShouldCompleteBattleByMusicEnd => musicEndBattleRequested && !WantsInfiniteRounds() && !stateMachine.IsBattleEndingOrFinished;

        public BattleFlowMusicEndHandler(BattleFlowStateMachine stateMachine, IAISetting aiSetting, IBattleJudge battleJudge, IBeatjudge beatJudge, IBattleOnlineSync battleOnlineSync, BattleFlowPauseHandler pauseHandler, Func<bool, bool> beginRoundResolution, Func<int> getCurrentRound, Func<CorePlayerId, IReadOnlyDictionary<CorePlayerId, int>, bool, Task> completeBattleWithWinnerAsync, Func<int> resolveTopHitPointPlayerId, Action<int, int, int, bool, int, bool, IReadOnlyDictionary<CorePlayerId, int>> publishRoundOutcome) {
            this.stateMachine = stateMachine;
            this.aiSetting = aiSetting;
            this.battleJudge = battleJudge;
            this.beatJudge = beatJudge;
            this.battleOnlineSync = battleOnlineSync;
            this.pauseHandler = pauseHandler;
            this.beginRoundResolution = beginRoundResolution;
            this.getCurrentRound = getCurrentRound;
            this.completeBattleWithWinnerAsync = completeBattleWithWinnerAsync;
            this.resolveTopHitPointPlayerId = resolveTopHitPointPlayerId;
            this.publishRoundOutcome = publishRoundOutcome;
        }

        public void Reset() {
            musicEndBattleRequested = false;
        }

        public void OnMusicPlaybackCompleted() {
            if (WantsInfiniteRounds() || stateMachine.IsBattleEndingOrFinished || musicEndBattleRequested) {
                return;
            }

            musicEndBattleRequested = true;
            Debug.Log($"{LOG_PREFIX} Music playback completed. state={stateMachine.Current}");

            if (!stateMachine.CanCompleteBattleByMusicEnd) {
                return;
            }

            _ = CompleteBattleByMusicEndAsync();
        }

        public void CompleteBattleByPendingMusicEndIfNeeded() {
            if (!ShouldCompleteBattleByMusicEnd || !stateMachine.CanCompleteBattleByMusicEnd) {
                return;
            }

            _ = CompleteBattleByMusicEndAsync();
        }

        public async Task CompleteBattleByMusicEndAsync() {
            try {
                if (!beginRoundResolution(false)) {
                    return;
                }

                beatJudge.ResetRoundState();
                pauseHandler.PresentRoundPlayableFinishToPlayers();

                var roundWins = battleJudge.GetRoundWins();
                var winner = ResolveMusicEndWinner(roundWins);
                // オンラインでも両ピアが publish を試み、BattleOnlineSync 側のマージで先着アウトカムに収束する。
                publishRoundOutcome(Math.Max(1, getCurrentRound()), -1, winner.Value, false, winner.Value, false, roundWins);
                await completeBattleWithWinnerAsync(winner, roundWins, false);
            }
            catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        public async Task CompleteBattleBySuspendMenuAsync() {
            try {
                if (!beginRoundResolution(true)) {
                    return;
                }

                beatJudge.ResetRoundState();
                pauseHandler.PresentRoundPlayableFinishToPlayers();

                var winnerPlayerId = resolveTopHitPointPlayerId();
                var roundWins = battleJudge.GetRoundWins();
                // サスペンド終了によるバトル終了も同様に対称送信（重複はマージで吸収）。
                publishRoundOutcome(Math.Max(1, getCurrentRound()), -1, winnerPlayerId, false, winnerPlayerId, true, roundWins);
                await completeBattleWithWinnerAsync(new CorePlayerId(winnerPlayerId), roundWins, true);
            }
            catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        public CorePlayerId ResolveMusicEndWinner(IReadOnlyDictionary<CorePlayerId, int> roundWins) {
            if (roundWins.Count > 0) {
                var highestWinCount = 0;
                foreach (var roundWin in roundWins) {
                    if (roundWin.Value > highestWinCount) {
                        highestWinCount = roundWin.Value;
                    }
                }

                if (highestWinCount > 0) {
                    CorePlayerId winner = new CorePlayerId(-1);
                    var leaderCount = 0;
                    foreach (var roundWin in roundWins) {
                        if (roundWin.Value == highestWinCount) {
                            winner = roundWin.Key;
                            leaderCount += 1;
                        }
                    }

                    if (leaderCount == 1) {
                        return winner;
                    }
                }
            }

            return new CorePlayerId(resolveTopHitPointPlayerId());
        }

        bool WantsInfiniteRounds() {
            return aiSetting.IsInfiniteRoundMode;
        }
    }
}
