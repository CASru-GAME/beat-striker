
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
        }

        void PrepareBattle() {
            if (battlePrepared) return;

            battlePrepared = true;
            battleDeployer.Deploy();
        }

        public void StartBattle() {
            if (battleStarted) return;

            PrepareBattle();
            SubscribeStrikerDeadEvents();
            SubscribeFlowEvents();
            battleStarted = true;
            Debug.Log("Battle Started".ToCyan());
            _ = StartBattleSequenceAsync();
        }

        async Task StartBattleSequenceAsync() {
            try {
                await sceneTransitionService.RequestEndTransitionAsync(ResolveCurrentBattleScene());
                await battlePresenter.PlayBattleOpeningAsync();
                SetAllStrikersDefault();

                await StartRoundPlayableAsync();
            }
            catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        AppScene ResolveCurrentBattleScene() {
            return battleSelectSetting.SelectedStage.CurrentValue == Stage.Street
                ? AppScene.Street
                : AppScene.Live;
        }

        async Task StartRoundPlayableAsync() {
            battlePresenter.CloseSuspendMenu();
            await battlePresenter.PlayRoundStartAsync(currentRound);
            roundStartedSubject.OnNext(currentRound);

            roundResolving = false;
            roundPlayable = true;
            roundSuspended = false;
            beatJudge.Resume();
            musicPlayer.Play();
            battleDeployer.ConnectRoundInputs();
            roundPlayableStartedSubject.OnNext(Unit.Default);
            battlePresenter.EnterRoundPlayablePhase();
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentRoundPlayableStart();
            }
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
            if (battleFinished || roundResolving || !roundPlayable) return;

            BeginRoundResolution();
            beatJudge.ResetRoundState();
            PresentRoundPlayableFinishToPlayers();

            _ = ResolveRoundAsync(deadPlayerId);
        }

        async Task ResolveRoundAsync(int deadPlayerId) {
            try {
                var finishedRound = currentRound;
                currentRound += 1;
                var roundResult = BuildRoundResult(finishedRound, deadPlayerId);
                var judgeResult = battleJudge.Judge(roundResult);

                if (judgeResult.ContinueBattle) {
                    roundFinishedSubject.OnNext(Unit.Default);
                    await battlePresenter.PlayRoundEndTransitionAsync();

                    battleDeployer.Undeploy();
                    battleDeployer.Deploy();
                    SubscribeStrikerDeadEvents();
                    SetAllStrikersDefault();
                    await battlePresenter.PlayRoundResumeTransitionAsync();
                    await StartRoundPlayableAsync();
                }
                else {
                    var winner = new CorePlayerId(judgeResult.Winner.Value);
                    await CompleteBattleWithWinnerAsync(winner);
                }
            }
            catch (Exception exception) {
                roundResolving = false;
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
            battleFinished = true;
            roundResolving = false;
            roundSuspended = false;
            battleFinishedSubject.OnNext(Unit.Default);
            outroStartedSubject.OnNext(winner);
            await battlePresenter.PlayBattleEndingAsync(winner);
            resultScene.ShowResult();
            await resultScene.WaitForBattleEndInputAsync();
            await battlePresenter.PlayBattleFinishFadeInAsync();
            CompleteBattle();
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
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                var subscription = striker.OnDead.Subscribe(_ => OnStrikerDead(striker.PlayerId.CurrentValue));
                deadEventDisposables.Add(subscription);
            }
        }

        void DisposeDeadEventSubscriptions() {
            foreach (var subscription in deadEventDisposables) {
                subscription.Dispose();
            }
            deadEventDisposables.Clear();
        }

        void SetAllStrikersDefault() {
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                striker.Default();
            }
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