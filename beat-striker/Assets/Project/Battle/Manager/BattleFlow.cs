
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
        int currentRound;
        bool battlePrepared;
        bool battleStarted;
        bool battleFinished;
        bool roundResolving;
        bool roundPlayable;
        bool roundSuspended;
        bool roundAttentionSuspended;
        bool roundTutorialSuspended;
        bool outroHandled;
        readonly List<IDisposable> deadEventDisposables = new();
        readonly List<IDisposable> flowEventDisposables = new();
        readonly HashSet<int> roundBaselineObjectInstanceIds = new();
        readonly List<GameObject> roundSpawnedRootObjects = new();
        bool hasRoundBaseline;
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
            battleStarted = false;
            battleFinished = false;
            roundResolving = false;
            roundPlayable = false;
            roundSuspended = false;
            roundAttentionSuspended = false;
            roundTutorialSuspended = false;
            outroHandled = false;
            activeRoundToken = 0;
            Debug.Log($"{LOG_PREFIX} Constructed. initialRound={currentRound}, playerPresenterCount={battlePlayerPresenters.Length}");
        }

        void PrepareBattle() {
            if (battlePrepared) {
                Debug.Log($"{LOG_PREFIX} PrepareBattle skipped because already prepared");
                return;
            }

            Debug.Log($"{LOG_PREFIX} PrepareBattle start");
            battlePrepared = true;
            battleDeployer.Deploy();
            Debug.Log($"{LOG_PREFIX} PrepareBattle completed and deploy requested");
        }

        public void StartBattle() {
            Debug.Log($"{LOG_PREFIX} StartBattle called. started={battleStarted}, prepared={battlePrepared}, finished={battleFinished}");
            if (battleStarted) {
                Debug.Log($"{LOG_PREFIX} StartBattle skipped because already started");
                return;
            }

            PrepareBattle();
            Debug.Log($"{LOG_PREFIX} StartBattle reset battle state");
            beatJudge.ResetBattleState();
            Debug.Log($"{LOG_PREFIX} StartBattle subscribe striker dead events");
            SubscribeStrikerDeadEvents();
            Debug.Log($"{LOG_PREFIX} StartBattle subscribe flow events");
            SubscribeFlowEvents();
            battleStarted = true;
            Debug.Log($"{LOG_PREFIX} StartBattle marked started=true and launching async sequence");
            _ = StartBattleSequenceAsync();
        }

        async Task StartBattleSequenceAsync() {
            try {
                var scene = ResolveCurrentBattleScene();
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync begin. targetScene={scene}");
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
            battlePresenter.CloseSuspendMenu();
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync closed suspend menu");
            await battlePresenter.PlayRoundStartAsync(currentRound);
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync round start animation completed. round={currentRound}");
            roundStartedSubject.OnNext(currentRound);

            roundResolving = false;
            roundPlayable = true;
            roundSuspended = false;
            roundAttentionSuspended = false;
            roundTutorialSuspended = false;
            beatJudge.ResetRoundState();
            ResumeRoundRuntimeSystems(controlsMusic: false);
            musicPlayer.Play();
            battleDeployer.ConnectRoundInputs();
            battleDeployer.BeginRoundEpisode(currentRound);
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync resumed systems and connected inputs");
            roundPlayableStartedSubject.OnNext(Unit.Default);
            battlePresenter.EnterRoundPlayablePhase();
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentRoundPlayableStart();
            }
            CaptureRoundBaselineObjects();
            StartLearningRoundTimeoutIfNeeded();
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync presenters notified. roundPlayable={roundPlayable}");
        }

        void CompleteBattle() {
            if (outroHandled) return;

            outroHandled = true;
            roundPlayable = false;
            roundSuspended = false;
            roundAttentionSuspended = false;
            roundTutorialSuspended = false;
            battlePresenter.CloseSuspendMenu();
            musicPlayer.Stop();
            battleDeployer.DisconnectRoundInputs();
            DisposeDeadEventSubscriptions();
            DisposeFlowEventSubscriptions();
            battleDeployer.Undeploy();
        }

        void OnStrikerDead(int deadPlayerId) {
            Debug.Log($"{LOG_PREFIX} OnStrikerDead received. deadPlayerId={deadPlayerId}, battleFinished={battleFinished}, roundResolving={roundResolving}, roundPlayable={roundPlayable}");
            if (battleFinished || roundResolving) {
                return;
            }

            if (!roundPlayable) {
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    return;
                }

                Debug.Log($"{LOG_PREFIX} OnStrikerDead accepted during tutorial pause. deadPlayerId={deadPlayerId}");
                tutorialSetting.ClearTutorialBattleRequest();
                _ = EndBattleToTitleAsync();
                return;
            }

            BeginRoundResolution();
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
                var continueBattle = WantsInfiniteRounds() || judgeResult.ContinueBattle;
                Debug.Log($"{LOG_PREFIX} ResolveRoundAsync judged. continueBattle={continueBattle}, winner={judgeResult.Winner}, mode={aiSetting.Mode.CurrentValue}");

                if (tutorialSetting.IsTutorialBattleRequested) {
                    Debug.Log($"{LOG_PREFIX} ResolveRoundAsync tutorial battle branch. end to title immediately");
                    roundResolving = false;
                    tutorialSetting.ClearTutorialBattleRequest();
                    await EndBattleToTitleAsync();
                    return;
                }

                if (continueBattle) {
                    roundFinishedSubject.OnNext(Unit.Default);
                    await battlePresenter.PlayRoundEndTransitionAsync();
                    DestroyRoundSpawnedObjects();

                    battleDeployer.RecordRoundResult(finishedRound, deadPlayerId);
                    battleDeployer.Undeploy();
                    battleDeployer.Deploy();
                    SubscribeStrikerDeadEvents();
                    SetAllStrikersDefault();
                    await battlePresenter.PlayRoundResumeTransitionAsync();
                    await StartRoundPlayableAsync();
                    Debug.Log($"{LOG_PREFIX} ResolveRoundAsync next round started. currentRound={currentRound}");
                }
                else {
                    var winner = new CorePlayerId(judgeResult.Winner.Value);
                    Debug.Log($"{LOG_PREFIX} ResolveRoundAsync battle end branch. winner={winner}");
                    await CompleteBattleWithWinnerAsync(winner, judgeResult.RoundWins);
                }
            }
            catch (Exception exception) {
                roundResolving = false;
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
            if (!roundPlayable || roundResolving || battleFinished || roundSuspended || roundAttentionSuspended || roundTutorialSuspended) {
                return;
            }

            battlePresenter.OpenSuspendMenu();
            PauseRoundForSuspendMenu();
        }

        void OnSuspendRequested() {
            if (battleFinished || roundResolving || !roundSuspended) {
                return;
            }

            if (tutorialSetting.IsTutorialBattleRequested) {
                tutorialSetting.ClearTutorialBattleRequest();
                _ = EndBattleToTitleAsync();
                return;
            }

            _ = CompleteBattleBySuspendMenuAsync();
        }

        async Task CompleteBattleBySuspendMenuAsync() {
            try {
                BeginRoundResolution();
                beatJudge.ResetRoundState();
                PresentRoundPlayableFinishToPlayers();

                var winnerPlayerId = ResolveTopHitPointPlayerId();

                await CompleteBattleWithWinnerAsync(new CorePlayerId(winnerPlayerId), battleJudge.GetRoundWins());
            }
            catch (Exception exception) {
                roundResolving = false;
                Debug.LogException(exception);
            }
        }

        async Task CompleteBattleWithWinnerAsync(CorePlayerId winner, IReadOnlyDictionary<CorePlayerId, int> roundWins) {
            Debug.Log($"{LOG_PREFIX} CompleteBattleWithWinnerAsync begin. winner={winner}");
            battleFinished = true;
            roundResolving = false;
            roundSuspended = false;
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

        void BeginRoundResolution() {
            activeRoundToken += 1;
            roundResolving = true;
            roundPlayable = false;
            roundSuspended = false;
            roundAttentionSuspended = false;
            roundTutorialSuspended = false;
            PauseRoundRuntimeSystems(controlsMusic: false);
            battlePresenter.CloseSuspendMenu();
            musicPlayer.Stop();
            battleDeployer.DisconnectRoundInputs();
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
            PauseRoundCommon(ref roundSuspended, controlsMusic: true);
        }

        void OnResumeRequested() {
            if (!roundSuspended || roundResolving || battleFinished) {
                return;
            }

            ResumeRoundCommon(ref roundSuspended, controlsMusic: true);
            battlePresenter.CloseSuspendMenu();
        }

        void OnAttentionActiveStateChanged(bool isActive) {
            if (battleFinished || roundResolving || !roundPlayable || roundSuspended || roundTutorialSuspended) {
                return;
            }

            if (isActive) {
                if (roundAttentionSuspended) {
                    return;
                }

                PauseRoundCommon(ref roundAttentionSuspended, controlsMusic: false);
                return;
            }

            if (!roundAttentionSuspended) {
                return;
            }

            ResumeRoundCommon(ref roundAttentionSuspended, controlsMusic: false);
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

        void CaptureRoundBaselineObjects() {
            roundBaselineObjectInstanceIds.Clear();
            var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < sceneObjects.Length; i++) {
                var gameObject = sceneObjects[i];
                if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) {
                    continue;
                }

                if ((gameObject.hideFlags & HideFlags.DontSave) != 0) {
                    continue;
                }

                roundBaselineObjectInstanceIds.Add(gameObject.GetInstanceID());
            }

            hasRoundBaseline = true;
            Debug.Log($"{LOG_PREFIX} CaptureRoundBaselineObjects completed. objectCount={roundBaselineObjectInstanceIds.Count}");
        }

        void DestroyRoundSpawnedObjects() {
            if (!hasRoundBaseline) {
                return;
            }

            roundSpawnedRootObjects.Clear();
            var roundSpawnedRootIds = new HashSet<int>();
            var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < sceneObjects.Length; i++) {
                var gameObject = sceneObjects[i];
                if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) {
                    continue;
                }

                if ((gameObject.hideFlags & HideFlags.DontSave) != 0) {
                    continue;
                }

                if (roundBaselineObjectInstanceIds.Contains(gameObject.GetInstanceID())) {
                    continue;
                }

                var root = gameObject.transform.root.gameObject;
                var rootId = root.GetInstanceID();
                if (roundBaselineObjectInstanceIds.Contains(rootId)) {
                    continue;
                }

                if (!roundSpawnedRootIds.Add(rootId)) {
                    continue;
                }

                roundSpawnedRootObjects.Add(root);
            }

            for (var i = 0; i < roundSpawnedRootObjects.Count; i++) {
                UnityEngine.Object.Destroy(roundSpawnedRootObjects[i]);
            }

            Debug.Log($"{LOG_PREFIX} DestroyRoundSpawnedObjects completed. destroyedCount={roundSpawnedRootObjects.Count}");
            roundSpawnedRootObjects.Clear();
            hasRoundBaseline = false;
            roundBaselineObjectInstanceIds.Clear();
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

                if (battleFinished || roundResolving) {
                    return;
                }

                if (roundPlayable) {
                    elapsedGameTime += Time.deltaTime;
                }
            }

            if (!WantsInfiniteRounds() || battleFinished || roundResolving || !roundPlayable) {
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
            if (!roundPlayable || roundResolving || battleFinished || roundSuspended || roundAttentionSuspended || roundTutorialSuspended) {
                return;
            }

            PauseRoundCommon(ref roundTutorialSuspended, controlsMusic: false);
            PresentTutorialPausedToPlayers();
            battlePresenter.CloseSuspendMenu();
        }

        public void ResumeRoundFromTutorial() {
            if (!roundTutorialSuspended || roundResolving || battleFinished || roundSuspended || roundAttentionSuspended) {
                return;
            }

            ResumeRoundCommon(ref roundTutorialSuspended, controlsMusic: false);
            PresentTutorialResumedToPlayers();
        }

        public async Task EndBattleToTitleAsync() {
            if (battleFinished || roundResolving) {
                return;
            }

            BeginRoundResolution();
            beatJudge.ResetRoundState();
            PresentRoundPlayableFinishToPlayers();
            battleFinished = true;
            roundResolving = false;
            roundSuspended = false;
            roundAttentionSuspended = false;
            roundTutorialSuspended = false;
            battleFinishedSubject.OnNext(Unit.Default);

            await battlePresenter.PlayBattleFinishFadeInAsync();
            CompleteBattle();

            var startResult = sceneTransitionService.RequestStartTransition(AppScene.Title);
            Debug.Log($"{LOG_PREFIX} EndBattleToTitleAsync start transition result. nextScene={AppScene.Title}, isSuccess={startResult.IsSuccess}");
        }

        void PauseRoundCommon(ref bool pauseFlag, bool controlsMusic) {
            if (pauseFlag) {
                return;
            }

            pauseFlag = true;
            roundPlayable = false;
            PauseRoundRuntimeSystems(controlsMusic);
        }

        void ResumeRoundCommon(ref bool pauseFlag, bool controlsMusic) {
            if (!pauseFlag) {
                return;
            }

            pauseFlag = false;
            if (roundResolving || battleFinished || IsAnyRoundPauseActive()) {
                return;
            }

            roundPlayable = true;
            ResumeRoundRuntimeSystems(controlsMusic);
        }

        bool IsAnyRoundPauseActive() {
            return roundSuspended || roundAttentionSuspended || roundTutorialSuspended;
        }
    }
}