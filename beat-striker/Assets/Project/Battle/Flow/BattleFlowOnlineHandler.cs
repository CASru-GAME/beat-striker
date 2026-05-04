using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CorePlayerId = App.PlayerId;

namespace Alice {
    /// <summary>
    /// オンライン対戦の「対称フロー」用の薄い窓口。ゲート通過・ラウンド開始の NetworkTime 合意・サスペンド/再開・アウトカム送信を集約する。
    /// ホスト(PlayerId==0)専用の分岐は排し、可能な限り battleOnlineSync へ対称 API を委譲する。
    /// </summary>
    public sealed class BattleFlowOnlineHandler {
        public const float RoundStartLeadSeconds = 1f;
        public const float RoundStartMinLeadSeconds = 0.05f;
        public const float ResumeLeadSeconds = 1f;
        public const float ResumeMinLeadSeconds = 0.05f;

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
        readonly IMusicPlayer musicPlayer;
        ulong lastAppliedPhaseSequence;
        ulong lastAppliedOutcomeSequence;

        public BattleFlowOnlineHandler(BattleFlowStateMachine stateMachine, IAppNetworkSetting appNetworkSetting, IBattleOnlineSync battleOnlineSync, IBattleJudge battleJudge, IBeatjudge beatJudge, BattleFlowPauseHandler pauseHandler, Func<bool, bool> beginRoundResolution, Func<CorePlayerId, IReadOnlyDictionary<CorePlayerId, int>, bool, Task> completeBattleWithWinnerAsync, Func<int> getCurrentRound, Action onSuspendMenuPause, Action onSuspendMenuResume, Func<Task> endBattleToTitleAsync, Func<int, Task> resolveRoundAsync, Action<int> onRoundResolutionRequested, IMusicPlayer musicPlayer) {
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
            this.musicPlayer = musicPlayer;
            lastAppliedPhaseSequence = 0;
            lastAppliedOutcomeSequence = 0;
        }

        public bool IsOnlineBattle => appNetworkSetting.IsOnline.CurrentValue && battleOnlineSync.IsReady;
        public bool IsOnlineHost => IsOnlineBattle && battleOnlineSync.IsSessionHost;
        public bool IsOnlineClient => IsOnlineBattle && !battleOnlineSync.IsSessionHost;

        /// <summary>オンライン時のみバリア待ち。オフラインは即完了。</summary>
        public Task PassFlowGateAsync(BattleFlowSyncGate gate, int round, int subIndex = 0) {
            return IsOnlineBattle ? battleOnlineSync.PassFlowGateAsync(gate, round, subIndex) : Task.CompletedTask;
        }

        public void PublishPhase(BattleFlowState state) {
            if (!IsOnlineBattle) {
                return;
            }

            battleOnlineSync.PublishPhase(state, getCurrentRound());
        }

        // オフライン互換用。オンライン本戦の足並みは FlowGate 側へ移しており、こちらは主にオフライン分岐から呼ばれる。
        public async Task WaitForHostPhaseAsync(BattleFlowState state, int round) {
            if (!IsOnlineBattle) {
                return;
            }

            await battleOnlineSync.WaitForPhaseAtLeastAsync(state, round);
        }

        public Task<BattleOutcomeSnapshot> WaitForOutcomeAsync(BattleOutcomeKind kind, int round) {
            return battleOnlineSync.WaitForOutcomeAsync(kind, round);
        }

        // 各ピアが ready を送り、双方揃ったあと同一の決定式で startNetworkTime を得る（片側スケジュール配信に依存しない）。
        public async Task<float> PrepareRoundPlaybackStartAsync(int round) {
            if (!IsOnlineBattle) {
                return 0f;
            }

            battleOnlineSync.PublishRoundStartReadyWithTime(round, battleOnlineSync.NetworkTime);
            return await battleOnlineSync.WaitSymmetricRoundStartNetworkTimeAsync(round, RoundStartLeadSeconds, RoundStartMinLeadSeconds);
        }

        // 合意した未来時刻に達するまで入力・音楽の本接続を遅らせ、先にゲートを抜けた側の不利を減らす。
        public async Task WaitForRoundPlaybackStartAsync(float startNetworkTime) {
            if (!IsOnlineBattle || startNetworkTime <= 0f) {
                return;
            }

            while (battleOnlineSync.NetworkTime < startNetworkTime) {
                await Task.Yield();
            }
        }

        public void ApplyOnlinePhaseSnapshot(BattleFlowPhaseSnapshot snapshot) {
            if (!IsOnlineBattle) {
                return;
            }

            if (snapshot.Sequence <= lastAppliedPhaseSequence) {
                return;
            }

            lastAppliedPhaseSequence = snapshot.Sequence;
            // 相手が先に解決フェーズへ入った場合の追従のみ残す（Suspended/Playing の同期はゲート・ビート側へ移行済み）。
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

            if (snapshot.State == BattleFlowState.EndingToTitle) {
                _ = endBattleToTitleAsync();
            }
        }

        public void ApplyOnlineOutcomeSnapshot(BattleOutcomeSnapshot outcome) {
            TryApplyBattleFinishedOutcome(outcome);
        }

        // ポーズメニュー操作を「この拍で適用」としてネットに載せ、BeatJudge がその拍でデュアル条件を評価できるようにする。
        public void PublishSuspendMenuBeatForCurrentTiming() {
            if (!IsOnlineBattle) {
                return;
            }

            var beatIndex = ResolveSuspendMenuApplyBeatIndex();
            battleOnlineSync.PublishSuspendMenuBeatRequest(beatIndex);
        }

        int ResolveSuspendMenuApplyBeatIndex() {
            if (beatJudge is BeatJudge concrete) {
                return concrete.GetSuspendMenuApplyBeatIndex();
            }

            var judged = musicPlayer.JudgeTiming(musicPlayer.CurrentPlaybackTime);
            return judged.BeatIndex + 1;
        }

        // 解除: 双方の ResumeAck を揃えたうえで resumeNetworkTime を決め、NetworkTime で待ってからローカル再開＋BeatSyncResume 通知＋解除ゲート。
        public async Task CompleteOnlineResumeFromSuspendMenuAsync() {
            if (!IsOnlineBattle) {
                onSuspendMenuResume();
                return;
            }

            battleOnlineSync.ClearResumeAckState();
            battleOnlineSync.PublishResumeAck(battleOnlineSync.NetworkTime);
            var resumeNetworkTime = await battleOnlineSync.WaitSymmetricResumeNetworkTimeAsync(ResumeLeadSeconds, ResumeMinLeadSeconds);
            while (battleOnlineSync.NetworkTime < resumeNetworkTime) {
                await Task.Yield();
            }

            var judged = musicPlayer.JudgeTiming(musicPlayer.CurrentPlaybackTime);
            battleOnlineSync.PublishBeatSyncResume(judged.BeatIndex, resumeNetworkTime, musicPlayer.CurrentPlaybackTime);
            battleOnlineSync.ClearResumeAckState();
            onSuspendMenuResume();
            await PassFlowGateAsync(BattleFlowSyncGate.SuspendMenuResumeClear, getCurrentRound(), judged.BeatIndex);
        }

        public void ApplyOnlineSuspendFinishRequest(Func<Task> completeBattleBySuspendMenuAsync) {
            if (!IsOnlineBattle || !stateMachine.CanSuspendBattle) {
                return;
            }

            _ = completeBattleBySuspendMenuAsync();
        }

        public void ApplyOnlineRoundResolutionRequest(int deadPlayerId) {
            if (!IsOnlineBattle || stateMachine.IsBattleEndingOrFinished || stateMachine.IsRoundResolving) {
                return;
            }

            onRoundResolutionRequested(deadPlayerId);
        }

        public void RequestSuspendFinish() {
            battleOnlineSync.RequestSuspendFinish();
        }

        public void RequestRoundResolution(int deadPlayerId) {
            battleOnlineSync.RequestRoundResolution(deadPlayerId);
        }

        // ネットで先に届いた BattleFinished を適用し、ローカルも同一終了チェーンへ乗せる（先着アウトカムの受け口）。
        public bool TryApplyBattleFinishedOutcome(BattleOutcomeSnapshot outcome) {
            if (!IsOnlineBattle || outcome.Kind != BattleOutcomeKind.BattleFinished || stateMachine.IsBattleEndingOrFinished) {
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

        // オンライン追従側が同じアウトカムを二重適用しないためのガード（シーケンスは BattleOnlineSync が採番した値）。
        public bool TryBeginApplyOutcomeSnapshot(BattleOutcomeSnapshot outcome) {
            if (!IsOnlineBattle) {
                return false;
            }

            if (outcome.Sequence <= lastAppliedOutcomeSequence) {
                return false;
            }

            lastAppliedOutcomeSequence = outcome.Sequence;
            return true;
        }

        // どちらのピアからでも送信可。BattleOnlineSync.TryMergeOutcomeAuthoritative で先着・矛盾排除されてからネットに載る。
        public void PublishRoundOutcome(int finishedRound, int deadPlayerId, int roundWinnerPlayerId, bool continueBattle, int finalWinnerPlayerId, bool stopMusic, IReadOnlyDictionary<CorePlayerId, int> roundWins) {
            if (!IsOnlineBattle) {
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
