
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App;
using UnityEngine;
using CorePlayerId = App.PlayerId;

namespace Alice {
    public class BattleFlow {
        const string LOG_PREFIX = "[BattleFlow]";
        const int TRAINING_ROUND_TIMEOUT_SECONDS = 60 * 7;
        readonly IBattleSetting battleSetting;
        readonly IBattleDeployer battleDeployer;
        readonly IStrikerRegistry strikerRegistry;
        readonly IBattleJudge battleJudge;
        readonly IBeatjudge beatJudge;
        readonly IMusicPlayer musicPlayer;
        readonly IAISetting aiSetting;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly ITutorialSetting tutorialSetting;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IBattlePresenter battlePresenter;
        readonly IBattleTutorialSignalEmitter tutorialSignalEmitter;
        readonly ResultScene resultScene;
        readonly IBattlePlayerPresenter[] battlePlayerPresenters;
        readonly BattleFlowStateMachine stateMachine = new();
        readonly RoundSceneSpawnTracker roundSceneSpawnTracker = new();
        int currentRound;
        bool battlePrepared;
        bool outroHandled;
        bool battleMusicStarted;
        bool musicEndBattleRequested;
        readonly List<IDisposable> deadEventDisposables = new();
        readonly List<IDisposable> flowEventDisposables = new();
        int activeRoundToken;

        readonly Subject<int> roundStartedSubject = new();
        readonly Subject<Unit> roundPlayableStartedSubject = new();
        readonly Subject<Unit> roundFinishedSubject = new();
        readonly Subject<Unit> battleFinishedSubject = new();
        readonly Subject<CorePlayerId> outroStartedSubject = new();

        public Observable<Unit> OnRoundPlayableStarted => roundPlayableStartedSubject;

        public BattleFlow(IBattleSetting battleSetting, IBattleDeployer battleDeployer, IStrikerRegistry strikerRegistry, IBattleJudge battleJudge, IBeatjudge beatJudge, IMusicPlayer musicPlayer, IAISetting aiSetting, IBattleSelectSetting battleSelectSetting, ITutorialSetting tutorialSetting, ISceneTransitionService sceneTransitionService, IBattlePresenter battlePresenter, IBattleTutorialSignalEmitter tutorialSignalEmitter, ResultScene resultScene, IBattlePlayerPresenter[] battlePlayerPresenters) {
            this.battleSetting = battleSetting;
            this.battleDeployer = battleDeployer;
            this.strikerRegistry = strikerRegistry;
            this.battleJudge = battleJudge;
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
            this.aiSetting = aiSetting;
            this.battleSelectSetting = battleSelectSetting;
            this.tutorialSetting = tutorialSetting;
            this.sceneTransitionService = sceneTransitionService;
            this.battlePresenter = battlePresenter;
            this.tutorialSignalEmitter = tutorialSignalEmitter;
            this.resultScene = resultScene;
            this.battlePlayerPresenters = battlePlayerPresenters;
            currentRound = 1;
            battlePrepared = false;
            outroHandled = false;
            battleMusicStarted = false;
            musicEndBattleRequested = false;
            activeRoundToken = 0;
            Debug.Log($"{LOG_PREFIX} Constructed. initialRound={currentRound}, playerPresenterCount={battlePlayerPresenters.Length}");
        }

        async Task PrepareBattleAsync() {
            if (battlePrepared) {
                Debug.Log($"{LOG_PREFIX} PrepareBattle skipped because already prepared");
                return;
            }

            Debug.Log($"{LOG_PREFIX} PrepareBattle start");
            battlePrepared = true;
            await battleDeployer.DeployAsync();
            Debug.Log($"{LOG_PREFIX} PrepareBattle completed and deploy requested");
        }

        public void StartBattle() {
            Debug.Log($"{LOG_PREFIX} StartBattle called. state={stateMachine.Current}, prepared={battlePrepared}");
            if (!stateMachine.TryStartBattle(nameof(StartBattle))) {
                Debug.Log($"{LOG_PREFIX} StartBattle skipped because state does not allow start. state={stateMachine.Current}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} StartBattle launching async sequence");
            _ = StartBattleSequenceAsync();
        }

        async Task StartBattleSequenceAsync() {
            try {
                var scene = ResolveCurrentBattleScene();
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync begin. targetScene={scene}");
                await PrepareBattleAsync();
                Debug.Log($"{LOG_PREFIX} StartBattle reset battle state");
                beatJudge.ResetBattleState();
                battlePresenter.HandleBattleStarted();
                battleMusicStarted = false;
                musicEndBattleRequested = false;
                Debug.Log($"{LOG_PREFIX} StartBattle subscribe striker dead events");
                SubscribeStrikerDeadEvents();
                Debug.Log($"{LOG_PREFIX} StartBattle subscribe flow events");
                SubscribeFlowEvents();
                var endResult = await sceneTransitionService.RequestEndTransitionAsync(ResolveCurrentBattleScene());
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync transition end completed. isSuccess={endResult.IsSuccess}");
                await battlePresenter.PlayBattleOpeningAsync();
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync battle opening completed");
                await System.Threading.Tasks.Task.WhenAll(battlePlayerPresenters.Select(battlePlayerPresenter => battlePlayerPresenter.PlayOpeningHpFillAsync()));
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync battle player HP opening animation completed");

                await StartRoundPlayableAsync();
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync completed first round playable start");
            }
            catch (Exception exception) {
                Debug.LogError($"{LOG_PREFIX} StartBattleSequenceAsync failed: {exception.Message}");
                Debug.LogException(exception);
            }
        }

        AppScene ResolveCurrentBattleScene() {
            return battleSelectSetting.SelectedStage.CurrentValue == Stage.Street
                ? AppScene.Street
                : AppScene.Live;
        }

        async Task StartRoundPlayableAsync() {
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync begin. round={currentRound}");
            if (!stateMachine.TryBeginRoundStarting(nameof(StartRoundPlayableAsync))) {
                return;
            }

            battlePresenter.CloseSuspendMenu();
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync closed suspend menu");
            await battlePresenter.PlayRoundStartAsync(currentRound);
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync round start animation completed. round={currentRound}");
            roundStartedSubject.OnNext(currentRound);

            if (!stateMachine.TryEnterPlaying($"{nameof(StartRoundPlayableAsync)} completed")) {
                return;
            }

            beatJudge.ResetRoundState();
            ResumeRoundRuntimeSystems(controlsMusic: false);
            if (!battleMusicStarted) {
                await musicPlayer.PlayAsync();
                battleMusicStarted = true;
            }
            battleDeployer.ConnectRoundInputs();
            battleDeployer.BeginRoundEpisode(currentRound);
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync resumed systems and connected inputs");
            roundPlayableStartedSubject.OnNext(Unit.Default);
            battlePresenter.EnterRoundPlayablePhase();
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentRoundPlayableStart();
            }
            roundSceneSpawnTracker.CaptureBaseline();
            StartLearningRoundTimeoutIfNeeded();
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync presenters notified. state={stateMachine.Current}");
        }

        void CompleteBattle() {
            if (outroHandled) return;

            outroHandled = true;
            stateMachine.TryMarkFinished(nameof(CompleteBattle));
            battlePresenter.CloseSuspendMenu();
            musicPlayer.Stop();
            battleMusicStarted = false;
            battleDeployer.DisconnectRoundInputs();
            DisposeDeadEventSubscriptions();
            DisposeFlowEventSubscriptions();
            battleDeployer.Undeploy();
        }

        void OnStrikerDead(int deadPlayerId) {
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
                _ = EndBattleToTitleAsync();
                return;
            }

            if (!BeginRoundResolution(stopMusic: false)) {
                return;
            }

            beatJudge.ResetRoundState();
            PresentRoundPlayableFinishToPlayers();

            _ = ResolveRoundAsync(deadPlayerId);
        }

        async Task ResolveRoundAsync(int deadPlayerId) {
            try {
                Debug.Log($"{LOG_PREFIX} ResolveRoundAsync begin. deadPlayerId={deadPlayerId}, currentRound={currentRound}");
                var finishedRound = currentRound;
                currentRound += 1;
                var roundResult = BuildRoundResult(finishedRound, deadPlayerId);
                var judgeResult = battleJudge.Judge(roundResult);
                var continueBattle = WantsInfiniteRounds() || !musicEndBattleRequested;
                Debug.Log($"{LOG_PREFIX} ResolveRoundAsync judged. continueBattle={continueBattle}, winner={judgeResult.Winner}, musicEnd={musicEndBattleRequested}, mode={aiSetting.Mode.CurrentValue}");

                if (tutorialSetting.IsTutorialBattleRequested) {
                    Debug.Log($"{LOG_PREFIX} ResolveRoundAsync tutorial battle branch. end to title immediately");
                    tutorialSetting.ClearTutorialBattleRequest();
                    await EndBattleToTitleAsync();
                    return;
                }

                var winnerPlayerId = deadPlayerId == 0 ? 1 : 0;
                var winnerRoundWinCount = judgeResult.RoundWins.TryGetValue(new CorePlayerId(winnerPlayerId), out var roundWinCount)
                    ? roundWinCount
                    : 0;
                battlePresenter.HandleRoundResolved(winnerPlayerId, winnerRoundWinCount, continueBattle);

                if (continueBattle) {
                    roundFinishedSubject.OnNext(Unit.Default);
                    await battlePresenter.PlayRoundEndTransitionAsync();
                    roundSceneSpawnTracker.DestroySpawnedObjects();
                    var winnerIfMusicEndsBeforeNextRound = ResolveMusicEndWinner(judgeResult.RoundWins);

                    if (ShouldCompleteBattleByMusicEnd()) {
                        Debug.Log($"{LOG_PREFIX} ResolveRoundAsync music ended during round transition. winner={winnerIfMusicEndsBeforeNextRound}");
                        await CompleteBattleWithWinnerAsync(winnerIfMusicEndsBeforeNextRound, judgeResult.RoundWins);
                        return;
                    }

                    battleDeployer.RecordRoundResult(finishedRound, deadPlayerId);
                    await battleDeployer.RedeployForNextRoundAsync();
                    SubscribeStrikerDeadEvents();
                    SetAllStrikersDefault();
                    await battlePresenter.PlayRoundResumeTransitionAsync();
                    if (ShouldCompleteBattleByMusicEnd()) {
                        Debug.Log($"{LOG_PREFIX} ResolveRoundAsync music ended before next round. winner={winnerIfMusicEndsBeforeNextRound}");
                        await CompleteBattleWithWinnerAsync(winnerIfMusicEndsBeforeNextRound, judgeResult.RoundWins);
                        return;
                    }

                    await StartRoundPlayableAsync();
                    if (ShouldCompleteBattleByMusicEnd()) {
                        Debug.Log($"{LOG_PREFIX} ResolveRoundAsync music ended during next round start. winner={winnerIfMusicEndsBeforeNextRound}");
                        await CompleteBattleWithWinnerAsync(winnerIfMusicEndsBeforeNextRound, judgeResult.RoundWins);
                        return;
                    }

                    Debug.Log($"{LOG_PREFIX} ResolveRoundAsync next round started. currentRound={currentRound}");
                }
                else {
                    var winner = ResolveMusicEndWinner(judgeResult.RoundWins);
                    Debug.Log($"{LOG_PREFIX} ResolveRoundAsync battle end branch. winner={winner}");
                    await CompleteBattleWithWinnerAsync(winner, judgeResult.RoundWins);
                }
            }
            catch (Exception exception) {
                Debug.LogError($"{LOG_PREFIX} ResolveRoundAsync failed: {exception.Message}");
                Debug.LogException(exception);
            }
        }

        void SubscribeFlowEvents() {
            DisposeFlowEventSubscriptions();
            flowEventDisposables.Add(battlePresenter.OnPauseMenuRequested.Subscribe(_ => OnPauseMenuRequested()));
            flowEventDisposables.Add(battlePresenter.OnSuspendRequested.Subscribe(_ => OnSuspendRequested()));
            flowEventDisposables.Add(battlePresenter.OnResumeRequested.Subscribe(_ => OnResumeRequested()));
            flowEventDisposables.Add(battlePresenter.OnAttentionActiveStateChanged.Subscribe(isActive => OnAttentionActiveStateChanged(isActive)));
            flowEventDisposables.Add(tutorialSignalEmitter.OnTutorialPauseRequested.Subscribe(_ => PauseRoundForTutorial()));
            flowEventDisposables.Add(tutorialSignalEmitter.OnTutorialResumeRequested.Subscribe(_ => ResumeRoundFromTutorial()));
            flowEventDisposables.Add(musicPlayer.OnPlaybackCompleted.Subscribe(_ => OnMusicPlaybackCompleted()));
            flowEventDisposables.Add(tutorialSignalEmitter.OnTutorialEndBattleToTitleRequested.Subscribe(_event => {
                _ = EndBattleToTitleAsync();
            }));
        }

        void DisposeFlowEventSubscriptions() {
            foreach (var subscription in flowEventDisposables) {
                subscription.Dispose();
            }
            flowEventDisposables.Clear();
        }

        void OnPauseMenuRequested() {
            if (!stateMachine.CanPauseForSuspend) {
                return;
            }

            battlePresenter.OpenSuspendMenu();
            PauseRoundForSuspendMenu();
        }

        void OnSuspendRequested() {
            if (!stateMachine.CanSuspendBattle) {
                return;
            }

            if (tutorialSetting.IsTutorialBattleRequested) {
                tutorialSetting.ClearTutorialBattleRequest();
                _ = EndBattleToTitleAsync();
                return;
            }

            _ = CompleteBattleBySuspendMenuAsync();
        }

        void OnMusicPlaybackCompleted() {
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

        async Task CompleteBattleByMusicEndAsync() {
            try {
                if (!BeginRoundResolution(stopMusic: false)) {
                    return;
                }

                beatJudge.ResetRoundState();
                PresentRoundPlayableFinishToPlayers();

                var roundWins = battleJudge.GetRoundWins();
                var winner = ResolveMusicEndWinner(roundWins);
                await CompleteBattleWithWinnerAsync(winner, roundWins);
            }
            catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        async Task CompleteBattleBySuspendMenuAsync() {
            try {
                if (!BeginRoundResolution(stopMusic: true)) {
                    return;
                }

                beatJudge.ResetRoundState();
                PresentRoundPlayableFinishToPlayers();

                var winnerPlayerId = ResolveTopHitPointPlayerId();

                await CompleteBattleWithWinnerAsync(new CorePlayerId(winnerPlayerId), battleJudge.GetRoundWins());
            }
            catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        bool ShouldCompleteBattleByMusicEnd() {
            return musicEndBattleRequested && !WantsInfiniteRounds() && !stateMachine.IsBattleEndingOrFinished;
        }

        void CompleteBattleByPendingMusicEndIfNeeded() {
            if (!ShouldCompleteBattleByMusicEnd() || !stateMachine.CanCompleteBattleByMusicEnd) {
                return;
            }

            _ = CompleteBattleByMusicEndAsync();
        }

        CorePlayerId ResolveMusicEndWinner(IReadOnlyDictionary<CorePlayerId, int> roundWins) {
            if (roundWins.Count > 0) {
                var highestWinCount = roundWins.Values.Max();
                var leaders = roundWins
                    .Where(roundWin => roundWin.Value == highestWinCount)
                    .Select(roundWin => roundWin.Key)
                    .ToList();

                if (highestWinCount > 0 && leaders.Count == 1) {
                    return leaders[0];
                }
            }

            return new CorePlayerId(ResolveTopHitPointPlayerId());
        }

        async Task CompleteBattleWithWinnerAsync(CorePlayerId winner, IReadOnlyDictionary<CorePlayerId, int> roundWins) {
            Debug.Log($"{LOG_PREFIX} CompleteBattleWithWinnerAsync begin. winner={winner}");
            if (!stateMachine.TryBeginEndingBattle(nameof(CompleteBattleWithWinnerAsync))) {
                return;
            }

            battleFinishedSubject.OnNext(Unit.Default);
            outroStartedSubject.OnNext(winner);
            await battlePresenter.PlayBattleEndingAsync(winner);
            var battleResults = beatJudge.GetBattleResults();
            resultScene.ShowResult(battleResults, roundWins);
            await resultScene.WaitForBattleEndInputAsync();
            await battlePresenter.PlayBattleFinishFadeInAsync();
            CompleteBattle();
            Debug.Log($"{LOG_PREFIX} CompleteBattleWithWinnerAsync completed. requesting start transition to ResultMenu");
            var startResult = sceneTransitionService.RequestStartTransition(AppScene.ResultMenu);
            Debug.Log($"{LOG_PREFIX} CompleteBattleWithWinnerAsync start transition result. nextScene={AppScene.ResultMenu}, isSuccess={startResult.IsSuccess}");
        }

        bool BeginRoundResolution(bool stopMusic) {
            if (!stateMachine.TryBeginResolvingRound(nameof(BeginRoundResolution))) {
                return false;
            }

            activeRoundToken += 1;
            PauseRoundRuntimeSystems(controlsMusic: false);
            battlePresenter.CloseSuspendMenu();
            if (stopMusic) {
                musicPlayer.Stop();
                battleMusicStarted = false;
            }
            battleDeployer.DisconnectRoundInputs();
            return true;
        }

        void PresentRoundPlayableFinishToPlayers() {
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentRoundPlayableFinish();
            }
        }

        void PresentTutorialPausedToPlayers() {
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentTutorialPause();
            }
        }

        void PresentTutorialResumedToPlayers() {
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentTutorialResume();
            }
        }

        int ResolveTopHitPointPlayerId() {
            return strikerRegistry.GetAllStrikers()
                .OrderByDescending(x => x.HitPoint.CurrentValue)
                .First()
                .PlayerId.CurrentValue;
        }

        void PauseRoundForSuspendMenu() {
            if (!stateMachine.TryBeginSuspend(nameof(PauseRoundForSuspendMenu))) {
                return;
            }

            PauseRoundRuntimeSystems(controlsMusic: true);
        }

        void OnResumeRequested() {
            if (!stateMachine.CanResumeFromSuspend) {
                return;
            }

            if (!stateMachine.TryEnterPlaying(nameof(OnResumeRequested))) {
                return;
            }

            ResumeRoundRuntimeSystems(controlsMusic: true);
            battlePresenter.CloseSuspendMenu();
            CompleteBattleByPendingMusicEndIfNeeded();
        }

        void OnAttentionActiveStateChanged(bool isActive) {
            if (stateMachine.IsBattleEndingOrFinished || stateMachine.IsRoundResolving || stateMachine.IsSuspended || stateMachine.IsTutorialSuspended) {
                return;
            }

            if (isActive) {
                if (!stateMachine.CanPauseForAttention) {
                    return;
                }

                if (!stateMachine.TryBeginAttentionSuspend(nameof(OnAttentionActiveStateChanged))) {
                    return;
                }

                PauseRoundRuntimeSystems(controlsMusic: false);
                return;
            }

            if (!stateMachine.CanResumeFromAttention) {
                return;
            }

            if (!stateMachine.TryEnterPlaying(nameof(OnAttentionActiveStateChanged))) {
                return;
            }

            ResumeRoundRuntimeSystems(controlsMusic: false);
            CompleteBattleByPendingMusicEndIfNeeded();
        }

        void PauseRoundRuntimeSystems(bool controlsMusic) {
            beatJudge.Pause();
            if (controlsMusic) {
                musicPlayer.Pause();
            }
            battleDeployer.PauseRound();
        }

        void ResumeRoundRuntimeSystems(bool controlsMusic) {
            beatJudge.Resume();
            if (controlsMusic) {
                musicPlayer.Resume();
            }
            battleDeployer.ResumeRound();
        }

        void SubscribeStrikerDeadEvents() {
            DisposeDeadEventSubscriptions();
            var count = 0;
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                var subscription = striker.OnDead.Subscribe(_ => OnStrikerDead(striker.PlayerId.CurrentValue));
                deadEventDisposables.Add(subscription);
                count += 1;
            }
            Debug.Log($"{LOG_PREFIX} SubscribeStrikerDeadEvents completed. subscriptionCount={count}");
        }

        void DisposeDeadEventSubscriptions() {
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

        public void PauseRoundForTutorial() {
            if (!stateMachine.CanPauseForTutorial) {
                return;
            }

            if (!stateMachine.TryBeginTutorialSuspend(nameof(PauseRoundForTutorial))) {
                return;
            }

            PauseRoundRuntimeSystems(controlsMusic: false);
            PresentTutorialPausedToPlayers();
            battlePresenter.CloseSuspendMenu();
        }

        public void ResumeRoundFromTutorial() {
            if (!stateMachine.CanResumeFromTutorial) {
                return;
            }

            if (!stateMachine.TryEnterPlaying(nameof(ResumeRoundFromTutorial))) {
                return;
            }

            ResumeRoundRuntimeSystems(controlsMusic: false);
            PresentTutorialResumedToPlayers();
            CompleteBattleByPendingMusicEndIfNeeded();
        }

        public async Task EndBattleToTitleAsync() {
            if (!stateMachine.CanBeginEndingToTitle) {
                return;
            }

            if (!stateMachine.TryBeginEndingToTitle(nameof(EndBattleToTitleAsync))) {
                return;
            }

            activeRoundToken += 1;
            PauseRoundRuntimeSystems(controlsMusic: false);
            battlePresenter.CloseSuspendMenu();
            musicPlayer.Stop();
            battleMusicStarted = false;
            battleDeployer.DisconnectRoundInputs();
            beatJudge.ResetRoundState();
            PresentRoundPlayableFinishToPlayers();
            battleFinishedSubject.OnNext(Unit.Default);

            await battlePresenter.PlayBattleFinishFadeInAsync();
            CompleteBattle();

            var startResult = sceneTransitionService.RequestStartTransition(AppScene.Title);
            Debug.Log($"{LOG_PREFIX} EndBattleToTitleAsync start transition result. nextScene={AppScene.Title}, isSuccess={startResult.IsSuccess}");
        }
    }
}