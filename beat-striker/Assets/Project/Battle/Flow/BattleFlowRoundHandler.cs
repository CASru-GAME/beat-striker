using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using CorePlayerId = App.PlayerId;

namespace Alice {
    public sealed class BattleFlowRoundHandler {
        const string LOG_PREFIX = "[BattleFlow]";
        const int TRAINING_ROUND_TIMEOUT_SECONDS = 60 * 7;
        readonly BattleFlowStateMachine stateMachine;
        readonly IStrikerRegistry strikerRegistry;
        readonly IBattleJudge battleJudge;
        readonly IBeatjudge beatJudge;
        readonly IBattleDeployer battleDeployer;
        readonly IBattlePresenter battlePresenter;
        readonly IBattlePlayerPresenter[] battlePlayerPresenters;
        readonly IAISetting aiSetting;
        readonly ITutorialSetting tutorialSetting;
        readonly IMusicPlayer musicPlayer;
        readonly BattleFlowPauseHandler pauseHandler;
        readonly BattleFlowMusicEndHandler musicEndHandler;
        readonly BattleFlowOnlineHandler onlineHandler;
        readonly RoundSceneSpawnTracker roundSceneSpawnTracker;
        readonly Func<BattleAddressablePreload> getBattleAddressablePreload;
        readonly Func<bool> getBattleMusicStarted;
        readonly Action<bool> setBattleMusicStarted;
        readonly Func<bool, bool> beginRoundResolution;
        readonly Func<Task> endBattleToTitleAsync;
        readonly Func<CorePlayerId, IReadOnlyDictionary<CorePlayerId, int>, bool, Task> completeBattleWithWinnerAsync;
        readonly Func<IReadOnlyDictionary<CorePlayerId, int>, CorePlayerId> resolveMusicEndWinner;
        readonly Func<bool> shouldCompleteBattleByMusicEnd;
        readonly Action<int> notifyRoundStarted;
        readonly Action notifyRoundPlayableStarted;
        readonly Action notifyRoundFinished;
        readonly List<IDisposable> deadEventDisposables = new();
        int currentRound;
        int activeRoundToken;

        public int CurrentRound => currentRound;

        public BattleFlowRoundHandler(BattleFlowStateMachine stateMachine, IStrikerRegistry strikerRegistry, IBattleJudge battleJudge, IBeatjudge beatJudge, IBattleDeployer battleDeployer, IBattlePresenter battlePresenter, IBattlePlayerPresenter[] battlePlayerPresenters, IAISetting aiSetting, ITutorialSetting tutorialSetting, IMusicPlayer musicPlayer, BattleFlowPauseHandler pauseHandler, BattleFlowMusicEndHandler musicEndHandler, BattleFlowOnlineHandler onlineHandler, RoundSceneSpawnTracker roundSceneSpawnTracker, Func<BattleAddressablePreload> getBattleAddressablePreload, Func<bool> getBattleMusicStarted, Action<bool> setBattleMusicStarted, Func<bool, bool> beginRoundResolution, Func<Task> endBattleToTitleAsync, Func<CorePlayerId, IReadOnlyDictionary<CorePlayerId, int>, bool, Task> completeBattleWithWinnerAsync, Func<IReadOnlyDictionary<CorePlayerId, int>, CorePlayerId> resolveMusicEndWinner, Func<bool> shouldCompleteBattleByMusicEnd, Action<int> notifyRoundStarted, Action notifyRoundPlayableStarted, Action notifyRoundFinished) {
            this.stateMachine = stateMachine;
            this.strikerRegistry = strikerRegistry;
            this.battleJudge = battleJudge;
            this.beatJudge = beatJudge;
            this.battleDeployer = battleDeployer;
            this.battlePresenter = battlePresenter;
            this.battlePlayerPresenters = battlePlayerPresenters;
            this.aiSetting = aiSetting;
            this.tutorialSetting = tutorialSetting;
            this.musicPlayer = musicPlayer;
            this.pauseHandler = pauseHandler;
            this.musicEndHandler = musicEndHandler;
            this.onlineHandler = onlineHandler;
            this.roundSceneSpawnTracker = roundSceneSpawnTracker;
            this.getBattleAddressablePreload = getBattleAddressablePreload;
            this.getBattleMusicStarted = getBattleMusicStarted;
            this.setBattleMusicStarted = setBattleMusicStarted;
            this.beginRoundResolution = beginRoundResolution;
            this.endBattleToTitleAsync = endBattleToTitleAsync;
            this.completeBattleWithWinnerAsync = completeBattleWithWinnerAsync;
            this.resolveMusicEndWinner = resolveMusicEndWinner;
            this.shouldCompleteBattleByMusicEnd = shouldCompleteBattleByMusicEnd;
            this.notifyRoundStarted = notifyRoundStarted;
            this.notifyRoundPlayableStarted = notifyRoundPlayableStarted;
            this.notifyRoundFinished = notifyRoundFinished;
            currentRound = 1;
            activeRoundToken = 0;
        }

        public void IncrementActiveRoundToken() {
            activeRoundToken += 1;
        }

        public async Task StartRoundPlayableAsync() {
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync begin. round={currentRound}");
            if (!stateMachine.TryBeginRoundStarting(nameof(StartRoundPlayableAsync))) {
                return;
            }

            // ラウンド1のみ: 開始合図アニメの手前で双方到達を取る（ゲート2）。
            if (onlineHandler.IsOnlineBattle && currentRound == 1) {
                await onlineHandler.PassFlowGateAsync(BattleFlowSyncGate.Round1BeforeStartCue, currentRound, 0);
            }

            // オンライン: RoundStart の前半（アニメ前）。オフラインは従来のフェーズ待ち相当（実質即時）。
            if (onlineHandler.IsOnlineBattle) {
                await onlineHandler.PassFlowGateAsync(BattleFlowSyncGate.RoundStart, currentRound, 0);
            }
            else {
                await onlineHandler.WaitForHostPhaseAsync(BattleFlowState.RoundStarting, currentRound);
            }

            battlePresenter.CloseSuspendMenu();
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync closed suspend menu");
            await battlePresenter.PlayRoundStartAsync(currentRound);
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync round start animation completed. round={currentRound}");
            if (shouldCompleteBattleByMusicEnd()) {
                Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync stopped before playable phase because music end is pending. round={currentRound}");
                return;
            }

            var playbackStartNetworkTime = await onlineHandler.PrepareRoundPlaybackStartAsync(currentRound);
            notifyRoundStarted(currentRound);

            if (!stateMachine.TryEnterPlaying($"{nameof(StartRoundPlayableAsync)} completed")) {
                return;
            }

            // オンライン: RoundStart の後半（再生直前）。その後に対称な startNetworkTime まで待つ。
            if (onlineHandler.IsOnlineBattle) {
                await onlineHandler.PassFlowGateAsync(BattleFlowSyncGate.RoundStart, currentRound, 1);
            }
            else {
                await onlineHandler.WaitForHostPhaseAsync(BattleFlowState.Playing, currentRound);
            }

            await onlineHandler.WaitForRoundPlaybackStartAsync(playbackStartNetworkTime);

            beatJudge.ResetRoundState();
            pauseHandler.ResumeRoundRuntimeSystems(controlsMusic: false);
            if (!getBattleMusicStarted()) {
                await musicPlayer.PlayAsync(getBattleAddressablePreload());
                setBattleMusicStarted(true);
            }
            battleDeployer.ConnectRoundInputs();
            battleDeployer.BeginRoundEpisode(currentRound);
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync resumed systems and connected inputs");
            notifyRoundPlayableStarted();
            battlePresenter.EnterRoundPlayablePhase();
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentRoundPlayableStart();
            }
            roundSceneSpawnTracker.CaptureBaseline();
            StartLearningRoundTimeoutIfNeeded();
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync presenters notified. state={stateMachine.Current}");
        }

        public void OnStrikerDead(int deadPlayerId) {
            Debug.Log($"{LOG_PREFIX} OnStrikerDead received. deadPlayerId={deadPlayerId}, state={stateMachine.Current}");
            if (stateMachine.IsBattleEndingOrFinished || stateMachine.IsRoundResolving) {
                return;
            }

            if (!stateMachine.IsPlaying) {
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    return;
                }

                Debug.Log($"{LOG_PREFIX} OnStrikerDead accepted during tutorial pause. deadPlayerId={deadPlayerId}");
                tutorialSetting.ClearTutorialBattleRequest();
                _ = endBattleToTitleAsync();
                return;
            }

            if (onlineHandler.IsOnlineClient) {
                onlineHandler.RequestRoundResolution(deadPlayerId);
                if (beginRoundResolution(false)) {
                    beatJudge.ResetRoundState();
                    pauseHandler.PresentRoundPlayableFinishToPlayers();
                    _ = ResolveRoundAsync(deadPlayerId);
                }
                return;
            }

            if (!beginRoundResolution(false)) {
                return;
            }

            beatJudge.ResetRoundState();
            pauseHandler.PresentRoundPlayableFinishToPlayers();

            _ = ResolveRoundAsync(deadPlayerId);
        }

        public async Task ResolveRoundAsync(int deadPlayerId) {
            try {
                Debug.Log($"{LOG_PREFIX} ResolveRoundAsync begin. deadPlayerId={deadPlayerId}, currentRound={currentRound}");
                var finishedRound = currentRound;
                currentRound += 1;
                if (onlineHandler.IsOnlineClient) {
                    await ResolveRoundFromHostOutcomeAsync(finishedRound);
                    return;
                }

                var roundResult = BuildRoundResult(finishedRound, deadPlayerId);
                var judgeResult = battleJudge.Judge(roundResult);
                var continueBattle = WantsInfiniteRounds() || !musicEndHandler.IsMusicEndBattleRequested;
                Debug.Log($"{LOG_PREFIX} ResolveRoundAsync judged. continueBattle={continueBattle}, winner={judgeResult.Winner}, musicEnd={musicEndHandler.IsMusicEndBattleRequested}, mode={aiSetting.Mode.CurrentValue}");

                if (tutorialSetting.IsTutorialBattleRequested) {
                    Debug.Log($"{LOG_PREFIX} ResolveRoundAsync tutorial battle branch. end to title immediately");
                    tutorialSetting.ClearTutorialBattleRequest();
                    await endBattleToTitleAsync();
                    return;
                }

                var winnerPlayerId = deadPlayerId == 0 ? 1 : 0;
                var winnerRoundWinCount = judgeResult.RoundWins.TryGetValue(new CorePlayerId(winnerPlayerId), out var roundWinCount)
                    ? roundWinCount
                    : 0;
                // オンラインではホスト限定にせず送信。先にマージされた内容が権威（遅延側の二重送信は BattleOnlineSync で弾かれる）。
                if (onlineHandler.IsOnlineBattle) {
                    var finalWinnerPlayerId = continueBattle
                        ? -1
                        : resolveMusicEndWinner(judgeResult.RoundWins).Value;
                    onlineHandler.PublishRoundOutcome(finishedRound, deadPlayerId, winnerPlayerId, continueBattle, finalWinnerPlayerId, false, judgeResult.RoundWins);
                }
                battlePresenter.HandleRoundResolved(winnerPlayerId, winnerRoundWinCount, continueBattle);

                if (continueBattle) {
                    notifyRoundFinished();
                    await battlePresenter.PlayRoundEndTransitionAsync();
                    roundSceneSpawnTracker.DestroySpawnedObjects();
                    var winnerIfMusicEndsBeforeNextRound = resolveMusicEndWinner(judgeResult.RoundWins);

                    if (shouldCompleteBattleByMusicEnd()) {
                        Debug.Log($"{LOG_PREFIX} ResolveRoundAsync music ended during round transition. winner={winnerIfMusicEndsBeforeNextRound}");
                        await CompleteBattleFromDarkRoundTransitionAsync(winnerIfMusicEndsBeforeNextRound, judgeResult.RoundWins);
                        return;
                    }

                    battleDeployer.RecordRoundResult(finishedRound, deadPlayerId);
                    await battleDeployer.RedeployForNextRoundAsync(getBattleAddressablePreload());
                    await Awaitable.NextFrameAsync();
                    if (shouldCompleteBattleByMusicEnd()) {
                        Debug.Log($"{LOG_PREFIX} ResolveRoundAsync music ended before round resume. winner={winnerIfMusicEndsBeforeNextRound}");
                        await CompleteBattleFromDarkRoundTransitionAsync(winnerIfMusicEndsBeforeNextRound, judgeResult.RoundWins);
                        return;
                    }

                    SubscribeStrikerDeadEvents();
                    SetAllStrikersDefault();
                    await battlePresenter.PlayRoundResumeTransitionAsync();
                    if (shouldCompleteBattleByMusicEnd()) {
                        Debug.Log($"{LOG_PREFIX} ResolveRoundAsync music ended during round resume. winner={winnerIfMusicEndsBeforeNextRound}");
                        await completeBattleWithWinnerAsync(winnerIfMusicEndsBeforeNextRound, judgeResult.RoundWins, false);
                        return;
                    }

                    // 次ラウンドの playable 開始に入る直前に、ホスト側もここでラウンド境界のバリアを取る（ゲート4）。
                    if (onlineHandler.IsOnlineBattle) {
                        await onlineHandler.PassFlowGateAsync(BattleFlowSyncGate.RoundEndToNextRound, finishedRound, 0);
                    }

                    await StartRoundPlayableAsync();
                    if (shouldCompleteBattleByMusicEnd()) {
                        Debug.Log($"{LOG_PREFIX} ResolveRoundAsync music ended during next round start. winner={winnerIfMusicEndsBeforeNextRound}");
                        await completeBattleWithWinnerAsync(winnerIfMusicEndsBeforeNextRound, judgeResult.RoundWins, false);
                        return;
                    }

                    Debug.Log($"{LOG_PREFIX} ResolveRoundAsync next round started. currentRound={currentRound}");
                }
                else {
                    var winner = resolveMusicEndWinner(judgeResult.RoundWins);
                    Debug.Log($"{LOG_PREFIX} ResolveRoundAsync battle end branch. winner={winner}");
                    await completeBattleWithWinnerAsync(winner, judgeResult.RoundWins, false);
                }
            }
            catch (Exception exception) {
                Debug.LogError($"{LOG_PREFIX} ResolveRoundAsync failed: {exception.Message}");
                Debug.LogException(exception);
            }
        }

        // オンラインで「判定ホスト」ではない側: 先にネットへ出たアウトカムを待ち、ローカル判定は行わず追従する。
        async Task ResolveRoundFromHostOutcomeAsync(int finishedRound) {
            var outcome = await onlineHandler.WaitForOutcomeAsync(BattleOutcomeKind.RoundResolved, finishedRound);
            if (outcome.Kind == BattleOutcomeKind.BattleFinished) {
                onlineHandler.TryApplyBattleFinishedOutcome(outcome);
                return;
            }

            if (!onlineHandler.TryBeginApplyOutcomeSnapshot(outcome)) {
                return;
            }

            var roundWins = BattleFlowOnlineHandler.BuildRoundWins(outcome.PlayerIds, outcome.RoundWinCounts);
            battleJudge.ApplyRoundWins(roundWins);

            if (outcome.Kind == BattleOutcomeKind.BattleFinished || !outcome.ContinueBattle) {
                var finalWinnerId = outcome.FinalWinnerPlayerId >= 0
                    ? outcome.FinalWinnerPlayerId
                    : outcome.RoundWinnerPlayerId;
                await completeBattleWithWinnerAsync(new CorePlayerId(finalWinnerId), roundWins, false);
                return;
            }

            var winnerRoundWinCount = roundWins.TryGetValue(new CorePlayerId(outcome.RoundWinnerPlayerId), out var roundWinCount)
                ? roundWinCount
                : 0;
            battlePresenter.HandleRoundResolved(outcome.RoundWinnerPlayerId, winnerRoundWinCount, outcome.ContinueBattle);
            notifyRoundFinished();
            await battlePresenter.PlayRoundEndTransitionAsync();
            roundSceneSpawnTracker.DestroySpawnedObjects();
            battleDeployer.RecordRoundResult(outcome.FinishedRound, outcome.DeadPlayerId);
            await battleDeployer.RedeployForNextRoundAsync(getBattleAddressablePreload());
            await Awaitable.NextFrameAsync();
            SubscribeStrikerDeadEvents();
            SetAllStrikersDefault();
            await battlePresenter.PlayRoundResumeTransitionAsync();
            // ホストの ResolveRoundAsync と同じゲートを通し、次ラウンド開始のタイミングを揃える。
            if (onlineHandler.IsOnlineBattle) {
                await onlineHandler.PassFlowGateAsync(BattleFlowSyncGate.RoundEndToNextRound, finishedRound, 0);
            }

            await StartRoundPlayableAsync();
            Debug.Log($"{LOG_PREFIX} ResolveRoundFromHostOutcomeAsync next round started. currentRound={currentRound}");
        }

        async Task CompleteBattleFromDarkRoundTransitionAsync(CorePlayerId winner, IReadOnlyDictionary<CorePlayerId, int> roundWins) {
            await battlePresenter.PlayBattleFinishFadeOutAsync();
            await completeBattleWithWinnerAsync(winner, roundWins, false);
        }

        public void SubscribeStrikerDeadEvents() {
            DisposeDeadEventSubscriptions();
            var count = 0;
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                var subscription = striker.OnDead.Subscribe(_ => OnStrikerDead(striker.PlayerId.CurrentValue));
                deadEventDisposables.Add(subscription);
                count += 1;
            }
            Debug.Log($"{LOG_PREFIX} SubscribeStrikerDeadEvents completed. subscriptionCount={count}");
        }

        public void DisposeDeadEventSubscriptions() {
            foreach (var subscription in deadEventDisposables) {
                subscription.Dispose();
            }
            deadEventDisposables.Clear();
        }

        void SetAllStrikersDefault() {
            var count = 0;
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                striker.Default();
                count += 1;
            }
            Debug.Log($"{LOG_PREFIX} SetAllStrikersDefault completed. strikerCount={count}");
        }

        RoundResult BuildRoundResult(int roundNumber, int deadPlayerId) {
            var strikers = strikerRegistry.GetAllStrikers().ToList();
            var deadHub = strikers.FirstOrDefault(x => x.PlayerId.CurrentValue == deadPlayerId);
            var aliveRankings = strikers
                .Where(x => x.PlayerId.CurrentValue != deadPlayerId)
                .OrderByDescending(x => x.HitPoint.CurrentValue)
                .ToList();

            var rankings = new List<PlayerRoundRank>(strikers.Count);
            for (int i = 0; i < aliveRankings.Count; i++) {
                rankings.Add(new PlayerRoundRank(new CorePlayerId(aliveRankings[i].PlayerId.CurrentValue), i + 1));
            }

            if (deadHub != null) {
                rankings.Add(new PlayerRoundRank(new CorePlayerId(deadHub.PlayerId.CurrentValue), strikers.Count));
            }

            return new RoundResult(roundNumber, rankings);
        }

        void StartLearningRoundTimeoutIfNeeded() {
            activeRoundToken += 1;
            if (!WantsInfiniteRounds()) {
                return;
            }

            var roundToken = activeRoundToken;
            _ = WatchLearningRoundTimeoutAsync(roundToken, currentRound);
        }

        async Task WatchLearningRoundTimeoutAsync(int roundToken, int roundNumberAtStart) {
            float elapsedGameTime = 0f;
            while (elapsedGameTime < TRAINING_ROUND_TIMEOUT_SECONDS) {
                await Task.Yield();

                if (roundToken != activeRoundToken) {
                    return;
                }

                if (stateMachine.IsBattleEndingOrFinished || stateMachine.IsRoundResolving) {
                    return;
                }

                if (stateMachine.IsPlaying) {
                    elapsedGameTime += Time.deltaTime;
                }
            }

            if (!WantsInfiniteRounds() || stateMachine.IsBattleEndingOrFinished || stateMachine.IsRoundResolving || !stateMachine.IsPlaying) {
                return;
            }

            var timeoutLoserPlayerId = ResolveLowestHitPointPlayerId();
            Debug.Log($"{LOG_PREFIX} Round timeout reached. round={roundNumberAtStart}, timeoutSeconds={TRAINING_ROUND_TIMEOUT_SECONDS}, loserPlayerId={timeoutLoserPlayerId}");
            OnStrikerDead(timeoutLoserPlayerId);
        }

        int ResolveLowestHitPointPlayerId() {
            return strikerRegistry.GetAllStrikers()
                .OrderBy(x => x.HitPoint.CurrentValue)
                .ThenByDescending(x => x.PlayerId.CurrentValue)
                .First()
                .PlayerId.CurrentValue;
        }

        bool WantsInfiniteRounds() {
            return aiSetting.IsInfiniteRoundMode;
        }

        public int ResolveTopHitPointPlayerId() {
            return strikerRegistry.GetAllStrikers()
                .OrderByDescending(x => x.HitPoint.CurrentValue)
                .First()
                .PlayerId.CurrentValue;
        }
    }
}
