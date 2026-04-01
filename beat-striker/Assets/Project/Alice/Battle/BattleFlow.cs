
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App;
using UnityEngine;
using CorePlayerId = Core.App.Types.PlayerId;

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
        readonly IBattlePresenter battlePresenter;
        readonly IBattlePlayerPresenter[] battlePlayerPresenters;
        int currentRound;
        bool battlePrepared;
        bool battleStarted;
        bool battleFinished;
        bool roundResolving;
        bool roundPlayable;
        bool outroHandled;
        readonly List<IDisposable> deadEventDisposables = new();

        readonly Subject<int> roundStartedSubject = new();
        readonly Subject<Unit> roundPlayableStartedSubject = new();
        readonly Subject<Unit> roundFinishedSubject = new();
        readonly Subject<Unit> battleFinishedSubject = new();
        readonly Subject<CorePlayerId> outroStartedSubject = new();

        public BattleFlow(IBattleDeployer battleDeployer, IStrikerRegistry strikerRegistry, IBattleJudge battleJudge, IBeatjudge beatJudge, IMusicPlayer musicPlayer, IBattlePresenter battlePresenter, IBattlePlayerPresenter[] battlePlayerPresenters) {
            this.battleDeployer = battleDeployer;
            this.strikerRegistry = strikerRegistry;
            this.battleJudge = battleJudge;
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
            this.battlePresenter = battlePresenter;
            this.battlePlayerPresenters = battlePlayerPresenters;
            currentRound = 1;
            battlePrepared = false;
            battleStarted = false;
            battleFinished = false;
            roundResolving = false;
            roundPlayable = false;
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
            battleStarted = true;
            Debug.Log("Battle Started".ToCyan());
            _ = StartBattleSequenceAsync();
        }

        async Task StartBattleSequenceAsync() {
            try {
                await battlePresenter.PlayBattleOpeningAsync();

                await StartRoundPlayableAsync();
            }
            catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        async Task StartRoundPlayableAsync() {
            await battlePresenter.PlayRoundStartAsync(currentRound);
            roundStartedSubject.OnNext(currentRound);

            roundResolving = false;
            roundPlayable = true;
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
            musicPlayer.Stop();
            battleDeployer.DisconnectRoundInputs();
            DisposeDeadEventSubscriptions();
            battleDeployer.Undeploy();
        }

        void OnStrikerDead(int deadPlayerId) {
            if (battleFinished || roundResolving || !roundPlayable) return;

            roundResolving = true;
            roundPlayable = false;
            musicPlayer.Stop();
            battleDeployer.DisconnectRoundInputs();
            beatJudge.ResetRoundState();
            foreach (var battlePlayerPresenter in battlePlayerPresenters) {
                battlePlayerPresenter.PresentRoundPlayableFinish();
            }

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
                    await battlePresenter.PlayRoundResumeTransitionAsync();
                    await StartRoundPlayableAsync();
                }
                else {
                    battleFinished = true;
                    roundResolving = false;
                    battleFinishedSubject.OnNext(Unit.Default);

                    var winner = judgeResult.Winner.Value;
                    outroStartedSubject.OnNext(winner);
                    await battlePresenter.PlayBattleEndingAsync(winner);
                    CompleteBattle();
                }
            }
            catch (Exception exception) {
                roundResolving = false;
                Debug.LogException(exception);
            }
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