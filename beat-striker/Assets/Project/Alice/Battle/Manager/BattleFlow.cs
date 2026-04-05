
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App;
using UnityEngine;
using CorePlayerId = App.PlayerId;

namespace Alice {
    public interface IBattleFlow {
        void StartBattle();
    }

    public class BattleFlow : IBattleFlow {
        const string LOG_PREFIX = "[BattleFlow]";
        readonly IBattleDeployer battleDeployer;
        readonly IStrikerRegistry strikerRegistry;
        readonly IBattleJudge battleJudge;
        readonly IBeatjudge beatJudge;
        readonly IMusicPlayer musicPlayer;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly ISceneTransitionService sceneTransitionService;
        readonly IBattlePresenter battlePresenter;
        readonly ResultScene resultScene;
        readonly IBattlePlayerPresenter[] battlePlayerPresenters;
        int currentRound;
        bool battlePrepared;
        bool battleStarted;
        bool battleFinished;
        bool roundResolving;
        bool roundPlayable;
        bool roundSuspended;
        bool outroHandled;
        readonly List<IDisposable> deadEventDisposables = new();
        readonly List<IDisposable> flowEventDisposables = new();

        readonly Subject<int> roundStartedSubject = new();
        readonly Subject<Unit> roundPlayableStartedSubject = new();
        readonly Subject<Unit> roundFinishedSubject = new();
        readonly Subject<Unit> battleFinishedSubject = new();
        readonly Subject<CorePlayerId> outroStartedSubject = new();

        public BattleFlow(IBattleDeployer battleDeployer, IStrikerRegistry strikerRegistry, IBattleJudge battleJudge, IBeatjudge beatJudge, IMusicPlayer musicPlayer, IBattleSelectSetting battleSelectSetting, ISceneTransitionService sceneTransitionService, IBattlePresenter battlePresenter, ResultScene resultScene, IBattlePlayerPresenter[] battlePlayerPresenters) {
            this.battleDeployer = battleDeployer;
            this.strikerRegistry = strikerRegistry;
            this.battleJudge = battleJudge;
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
            this.battleSelectSetting = battleSelectSetting;
            this.sceneTransitionService = sceneTransitionService;
            this.battlePresenter = battlePresenter;
            this.resultScene = resultScene;
            this.battlePlayerPresenters = battlePlayerPresenters;
            currentRound = 1;
            battlePrepared = false;
            battleStarted = false;
            battleFinished = false;
            roundResolving = false;
            roundPlayable = false;
            roundSuspended = false;
            outroHandled = false;
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
                await sceneTransitionService.RequestEndTransitionAsync(ResolveCurrentBattleScene());
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync transition end completed");
                await battlePresenter.PlayBattleOpeningAsync();
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync battle opening completed");
                SetAllStrikersDefault();
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync set all strikers default completed");

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
            beatJudge.Resume();
            musicPlayer.Play();
            battleDeployer.ConnectRoundInputs();
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync resumed systems and connected inputs");
            roundPlayableStartedSubject.OnNext(Unit.Default);
            battlePresenter.EnterRoundPlayablePhase();
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentRoundPlayableStart();
            }
            Debug.Log($"{LOG_PREFIX} StartRoundPlayableAsync presenters notified. roundPlayable={roundPlayable}");
        }

        void CompleteBattle() {
            if (outroHandled) return;

            outroHandled = true;
            roundPlayable = false;
            roundSuspended = false;
            beatJudge.Resume();
            battlePresenter.CloseSuspendMenu();
            musicPlayer.Stop();
            battleDeployer.DisconnectRoundInputs();
            DisposeDeadEventSubscriptions();
            DisposeFlowEventSubscriptions();
            battleDeployer.Undeploy();
        }

        void OnStrikerDead(int deadPlayerId) {
            Debug.Log($"{LOG_PREFIX} OnStrikerDead received. deadPlayerId={deadPlayerId}, battleFinished={battleFinished}, roundResolving={roundResolving}, roundPlayable={roundPlayable}");
            if (battleFinished || roundResolving || !roundPlayable) return;

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
                Debug.Log($"{LOG_PREFIX} ResolveRoundAsync judged. continueBattle={judgeResult.ContinueBattle}, winner={judgeResult.Winner}");

                if (judgeResult.ContinueBattle) {
                    roundFinishedSubject.OnNext(Unit.Default);
                    await battlePresenter.PlayRoundEndTransitionAsync();

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
                    await CompleteBattleWithWinnerAsync(winner);
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
        }

        void DisposeFlowEventSubscriptions() {
            foreach (var subscription in flowEventDisposables) {
                subscription.Dispose();
            }
            flowEventDisposables.Clear();
        }

        void OnPauseMenuRequested() {
            if (!roundPlayable || roundResolving || battleFinished || roundSuspended) {
                return;
            }

            battlePresenter.OpenSuspendMenu();
            PauseRoundForSuspendMenu();
        }

        void OnSuspendRequested() {
            if (battleFinished || roundResolving || !roundSuspended) {
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

                await CompleteBattleWithWinnerAsync(new CorePlayerId(winnerPlayerId));
            }
            catch (Exception exception) {
                roundResolving = false;
                Debug.LogException(exception);
            }
        }

        async Task CompleteBattleWithWinnerAsync(CorePlayerId winner) {
            Debug.Log($"{LOG_PREFIX} CompleteBattleWithWinnerAsync begin. winner={winner}");
            battleFinished = true;
            roundResolving = false;
            roundSuspended = false;
            battleFinishedSubject.OnNext(Unit.Default);
            outroStartedSubject.OnNext(winner);
            await battlePresenter.PlayBattleEndingAsync(winner);
            var battleResults = beatJudge.GetBattleResults();
            resultScene.ShowResult(battleResults);
            await resultScene.WaitForBattleEndInputAsync();
            await battlePresenter.PlayBattleFinishFadeInAsync();
            CompleteBattle();
            Debug.Log($"{LOG_PREFIX} CompleteBattleWithWinnerAsync completed. requesting start transition to ResultMenu");
            _ = sceneTransitionService.RequestStartTransition(AppScene.ResultMenu);
        }

        void BeginRoundResolution() {
            roundResolving = true;
            roundPlayable = false;
            roundSuspended = false;
            beatJudge.Resume();
            battlePresenter.CloseSuspendMenu();
            musicPlayer.Stop();
            battleDeployer.DisconnectRoundInputs();
        }

        void PresentRoundPlayableFinishToPlayers() {
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentRoundPlayableFinish();
            }
        }

        int ResolveTopHitPointPlayerId() {
            return strikerRegistry.GetAllStrikers()
                .OrderByDescending(x => x.HitPoint.CurrentValue)
                .First()
                .PlayerId.CurrentValue;
        }

        void PauseRoundForSuspendMenu() {
            if (roundSuspended) {
                return;
            }

            roundSuspended = true;
            roundPlayable = false;
            beatJudge.Pause();
            musicPlayer.Pause();
            battleDeployer.PauseRound();
        }

        void OnResumeRequested() {
            if (!roundSuspended || roundResolving || battleFinished) {
                return;
            }

            roundSuspended = false;
            roundPlayable = true;
            battlePresenter.CloseSuspendMenu();
            beatJudge.Resume();
            musicPlayer.Resume();
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
    }
}