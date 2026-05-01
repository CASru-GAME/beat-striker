using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CorePlayerId = App.PlayerId;

namespace Alice {
    public sealed class BattleFlowOnlineHandler {
        readonly BattleFlowStateMachine stateMachine;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IBattleOnlineSync battleOnlineSync;
        readonly IBattleJudge battleJudge;
        readonly IBeatjudge beatJudge;
        readonly BattleFlowPauseHandler pauseHandler;
        readonly Func<bool, bool> beginRoundResolution;
        readonly Func<CorePlayerId, IReadOnlyDictionary<CorePlayerId, int>, bool, Task> completeBattleWithWinnerAsync;
        readonly Func<int> getCurrentRound;
        readonly Action onSuspendMenuPause;
        readonly Action onSuspendMenuResume;
        readonly Func<Task> endBattleToTitleAsync;
        readonly Func<int, Task> resolveRoundAsync;
        readonly Action<int> onRoundResolutionRequested;
        ulong lastAppliedPhaseSequence;
        ulong lastAppliedOutcomeSequence;

        public BattleFlowOnlineHandler(BattleFlowStateMachine stateMachine, IAppNetworkSetting appNetworkSetting, IBattleOnlineSync battleOnlineSync, IBattleJudge battleJudge, IBeatjudge beatJudge, BattleFlowPauseHandler pauseHandler, Func<bool, bool> beginRoundResolution, Func<CorePlayerId, IReadOnlyDictionary<CorePlayerId, int>, bool, Task> completeBattleWithWinnerAsync, Func<int> getCurrentRound, Action onSuspendMenuPause, Action onSuspendMenuResume, Func<Task> endBattleToTitleAsync, Func<int, Task> resolveRoundAsync, Action<int> onRoundResolutionRequested) {
            this.stateMachine = stateMachine;
            this.appNetworkSetting = appNetworkSetting;
            this.battleOnlineSync = battleOnlineSync;
            this.battleJudge = battleJudge;
            this.beatJudge = beatJudge;
            this.pauseHandler = pauseHandler;
            this.beginRoundResolution = beginRoundResolution;
            this.completeBattleWithWinnerAsync = completeBattleWithWinnerAsync;
            this.getCurrentRound = getCurrentRound;
            this.onSuspendMenuPause = onSuspendMenuPause;
            this.onSuspendMenuResume = onSuspendMenuResume;
            this.endBattleToTitleAsync = endBattleToTitleAsync;
            this.resolveRoundAsync = resolveRoundAsync;
            this.onRoundResolutionRequested = onRoundResolutionRequested;
            lastAppliedPhaseSequence = 0;
            lastAppliedOutcomeSequence = 0;
        }

        public bool IsOnlineBattle => appNetworkSetting.IsOnline.CurrentValue && battleOnlineSync.IsReady;
        public bool IsOnlineHost => IsOnlineBattle && battleOnlineSync.IsSessionHost;
        public bool IsOnlineClient => IsOnlineBattle && !battleOnlineSync.IsSessionHost;

        public void PublishPhase(BattleFlowState state) {
            if (!IsOnlineHost) {
                return;
            }

            battleOnlineSync.PublishPhase(state, getCurrentRound());
        }

        public async Task WaitForHostPhaseAsync(BattleFlowState state, int round) {
            if (!IsOnlineClient) {
                return;
            }

            await battleOnlineSync.WaitForPhaseAtLeastAsync(state, round);
        }

        public Task<BattleOutcomeSnapshot> WaitForOutcomeAsync(BattleOutcomeKind kind, int round) {
            return battleOnlineSync.WaitForOutcomeAsync(kind, round);
        }

        public void ApplyOnlinePhaseSnapshot(BattleFlowPhaseSnapshot snapshot) {
            if (!IsOnlineClient) {
                return;
            }

            if (snapshot.Sequence <= lastAppliedPhaseSequence) {
                return;
            }

            lastAppliedPhaseSequence = snapshot.Sequence;
            if (snapshot.State == BattleFlowState.Suspended) {
                onSuspendMenuPause();
                return;
            }

            if (snapshot.State == BattleFlowState.Playing && stateMachine.CanResumeFromSuspend) {
                onSuspendMenuResume();
                return;
            }

            if (snapshot.State == BattleFlowState.ResolvingRound
                && !stateMachine.IsRoundResolving
                && !stateMachine.IsBattleEndingOrFinished) {
                if (beginRoundResolution(false)) {
                    beatJudge.ResetRoundState();
                    pauseHandler.PresentRoundPlayableFinishToPlayers();
                    _ = resolveRoundAsync(-1);
                }
                return;
            }

            if ((snapshot.State == BattleFlowState.EndingBattle || snapshot.State == BattleFlowState.Finished)
                && !stateMachine.IsBattleEndingOrFinished) {
                _ = WaitAndApplyBattleFinishedOutcomeAsync(snapshot.Round);
                return;
            }

            if (snapshot.State == BattleFlowState.EndingToTitle) {
                _ = endBattleToTitleAsync();
            }
        }

        async Task WaitAndApplyBattleFinishedOutcomeAsync(int round) {
            try {
                var outcome = await battleOnlineSync.WaitForOutcomeAsync(BattleOutcomeKind.BattleFinished, round);
                TryApplyBattleFinishedOutcome(outcome);
            }
            catch {
                // Outcome wait failure is handled by disconnect flow elsewhere.
            }
        }

        public void ApplyOnlineOutcomeSnapshot(BattleOutcomeSnapshot outcome) {
            TryApplyBattleFinishedOutcome(outcome);
        }

        public void ApplyOnlinePauseRequest() {
            if (!IsOnlineHost) {
                return;
            }

            onSuspendMenuPause();
        }

        public void ApplyOnlineResumeRequest() {
            if (!IsOnlineHost) {
                return;
            }

            onSuspendMenuResume();
        }

        public void ApplyOnlineSuspendFinishRequest(Func<Task> completeBattleBySuspendMenuAsync) {
            if (!IsOnlineHost || !stateMachine.CanSuspendBattle) {
                return;
            }

            _ = completeBattleBySuspendMenuAsync();
        }

        public void ApplyOnlineRoundResolutionRequest(int deadPlayerId) {
            if (!IsOnlineHost || stateMachine.IsBattleEndingOrFinished || stateMachine.IsRoundResolving) {
                return;
            }

            onRoundResolutionRequested(deadPlayerId);
        }

        public void RequestPause() {
            battleOnlineSync.RequestPause();
        }

        public void RequestResume() {
            battleOnlineSync.RequestResume();
        }

        public void RequestSuspendFinish() {
            battleOnlineSync.RequestSuspendFinish();
        }

        public void RequestRoundResolution(int deadPlayerId) {
            battleOnlineSync.RequestRoundResolution(deadPlayerId);
        }

        public bool TryApplyBattleFinishedOutcome(BattleOutcomeSnapshot outcome) {
            if (!IsOnlineClient || outcome.Kind != BattleOutcomeKind.BattleFinished || stateMachine.IsBattleEndingOrFinished) {
                return false;
            }

            if (outcome.Sequence <= lastAppliedOutcomeSequence) {
                return false;
            }

            lastAppliedOutcomeSequence = outcome.Sequence;
            var roundWins = BuildRoundWins(outcome.PlayerIds, outcome.RoundWinCounts);
            battleJudge.ApplyRoundWins(roundWins);
            var finalWinnerId = outcome.FinalWinnerPlayerId >= 0
                ? outcome.FinalWinnerPlayerId
                : outcome.RoundWinnerPlayerId;

            if (!stateMachine.IsRoundResolving) {
                if (!beginRoundResolution(outcome.StopMusic)) {
                    return false;
                }

                beatJudge.ResetRoundState();
                pauseHandler.PresentRoundPlayableFinishToPlayers();
            }

            _ = completeBattleWithWinnerAsync(new CorePlayerId(finalWinnerId), roundWins, false);
            return true;
        }

        public bool TryBeginApplyOutcomeSnapshot(BattleOutcomeSnapshot outcome) {
            if (!IsOnlineClient) {
                return false;
            }

            if (outcome.Sequence <= lastAppliedOutcomeSequence) {
                return false;
            }

            lastAppliedOutcomeSequence = outcome.Sequence;
            return true;
        }

        public void PublishRoundOutcome(int finishedRound, int deadPlayerId, int roundWinnerPlayerId, bool continueBattle, int finalWinnerPlayerId, bool stopMusic, IReadOnlyDictionary<CorePlayerId, int> roundWins) {
            if (!IsOnlineHost) {
                return;
            }

            var playerIds = new int[roundWins.Count];
            var roundWinCounts = new int[roundWins.Count];
            var index = 0;
            foreach (var roundWin in roundWins) {
                playerIds[index] = roundWin.Key.Value;
                roundWinCounts[index] = roundWin.Value;
                index += 1;
            }

            battleOnlineSync.PublishOutcome(new BattleOutcomeSnapshot(
                0,
                continueBattle ? BattleOutcomeKind.RoundResolved : BattleOutcomeKind.BattleFinished,
                finishedRound,
                deadPlayerId,
                roundWinnerPlayerId,
                continueBattle,
                finalWinnerPlayerId,
                stopMusic,
                playerIds,
                roundWinCounts));
        }

        public static IReadOnlyDictionary<CorePlayerId, int> BuildRoundWins(int[] playerIds, int[] roundWinCounts) {
            var roundWins = new Dictionary<CorePlayerId, int>();
            var count = Math.Min(playerIds.Length, roundWinCounts.Length);
            for (var i = 0; i < count; i++) {
                roundWins[new CorePlayerId(playerIds[i])] = roundWinCounts[i];
            }

            return roundWins;
        }
    }
}
