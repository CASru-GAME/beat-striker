
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using App;
using UnityEngine;
using CorePlayerId = Core.App.Types.PlayerId;

namespace Alice {
    public interface IBattleFlow {
        Observable<int> RoundStarted { get; }
        Observable<Unit> RoundPlayableStarted { get; }
        Observable<Unit> RoundFinished { get; }
        Observable<Unit> BattleFinished { get; }
        Observable<CorePlayerId> OutroStarted { get; }

        void PrepareBattle();
        void StartBattle();
    }

    public class BattleFlow : IBattleFlow {
        readonly IBattleDeployer battleDeployer;
        readonly IStrikerRegistry strikerRegistry;
        readonly IBattleJudge battleJudge;
        readonly IMusicPlayer musicPlayer;
        readonly IBattlePresenter battlePresenter;
        readonly IBattlePlayerPresenter[] battlePlayerPresenters;
        int currentRound;
        bool battlePrepared;
        bool battleStarted;
        bool battleFinished;
        bool roundResolving;
        bool waitingForRoundStartAnimation;
        BattleJudgeResult pendingJudgeResult;
        readonly List<IDisposable> deadEventDisposables = new();

        readonly Subject<int> roundStartedSubject = new();
        readonly Subject<Unit> roundPlayableStartedSubject = new();
        readonly Subject<Unit> roundFinishedSubject = new();
        readonly Subject<Unit> battleFinishedSubject = new();
        readonly Subject<CorePlayerId> outroStartedSubject = new();
        readonly CompositeDisposable presenterDisposables = new();

        public Observable<int> RoundStarted => roundStartedSubject;
        public Observable<Unit> RoundPlayableStarted => roundPlayableStartedSubject;
        public Observable<Unit> RoundFinished => roundFinishedSubject;
        public Observable<Unit> BattleFinished => battleFinishedSubject;
        public Observable<CorePlayerId> OutroStarted => outroStartedSubject;

        public BattleFlow(IBattleDeployer battleDeployer, IStrikerRegistry strikerRegistry, IBattleJudge battleJudge, IMusicPlayer musicPlayer, IBattlePresenter battlePresenter, IBattlePlayerPresenter[] strikerUIPresenters) {
            this.battleDeployer = battleDeployer;
            this.strikerRegistry = strikerRegistry;
            this.battleJudge = battleJudge;
            this.musicPlayer = musicPlayer;
            this.battlePresenter = battlePresenter;
            this.battlePlayerPresenters = strikerUIPresenters;
            currentRound = 1;
            battlePrepared = false;
            battleStarted = false;
            battleFinished = false;
            roundResolving = false;
            waitingForRoundStartAnimation = false;

            battlePresenter.IntroFinished
                .Subscribe(_ => OnIntroPresentationFinished())
                .AddTo(presenterDisposables);

            battlePresenter.RoundStartAnimationFinished
                .Subscribe(_ => OnRoundStartPresentationFinished())
                .AddTo(presenterDisposables);

            battlePresenter.RoundFinishAnimationFinished
                .Subscribe(_ => OnRoundFinishPresentationFinished())
                .AddTo(presenterDisposables);

            battlePresenter.OutroFinished
                .Subscribe(_ => OnOutroPresentationFinished())
                .AddTo(presenterDisposables);
        }

        public void PrepareBattle() {
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
            battlePresenter.PresentIntro();
        }

        void OnIntroPresentationFinished() {
            if (!battleStarted) return;

            musicPlayer.Play();
            waitingForRoundStartAnimation = true;
            battlePresenter.PresentRoundStart(currentRound);
            roundStartedSubject.OnNext(currentRound);
        }

        void OnRoundStartPresentationFinished() {
            if (!waitingForRoundStartAnimation) return;

            waitingForRoundStartAnimation = false;
            roundResolving = false;
            battleDeployer.ConnectRoundInputs();
            roundPlayableStartedSubject.OnNext(Unit.Default);
            battlePresenter.PresentRoundPlayableStart();
            foreach (var strikerUIPresenter in battlePlayerPresenters) {
                strikerUIPresenter.PresentRoundPlayableStart();
            }
        }

        void OnRoundFinishPresentationFinished() {
            if (!roundResolving || pendingJudgeResult == null) return;

            if (pendingJudgeResult.ContinueBattle) {
                battleDeployer.Deploy();
                SubscribeStrikerDeadEvents();
                waitingForRoundStartAnimation = true;
                pendingJudgeResult = null;
                battlePresenter.PresentRoundStart(currentRound);
                roundStartedSubject.OnNext(currentRound);
                return;
            }

            battleFinished = true;
            var winner = pendingJudgeResult.Winner.Value;
            roundResolving = false;
            waitingForRoundStartAnimation = false;
            pendingJudgeResult = null;
            outroStartedSubject.OnNext(winner);
            battlePresenter.PresentOutro(winner);
        }

        void OnOutroPresentationFinished() {
            battleDeployer.Undeploy();
        }

        void OnStrikerDead(CorePlayerId deadPlayerId) {
            if (battleFinished || roundResolving) return;

            roundResolving = true;
            battleDeployer.DisconnectRoundInputs();
            var finishedRound = currentRound;
            currentRound += 1;
            var roundResult = BuildRoundResult(finishedRound, deadPlayerId);
            pendingJudgeResult = battleJudge.Judge(roundResult);
            if (pendingJudgeResult.ContinueBattle) {
                battleDeployer.Undeploy();
                roundFinishedSubject.OnNext(Unit.Default);
                battlePresenter.PresentRoundFinish();
            }
            else {
                battleFinishedSubject.OnNext(Unit.Default);
                battlePresenter.PresentBattleFinish();
            }
        }

        void SubscribeStrikerDeadEvents() {
            DisposeDeadEventSubscriptions();
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                var subscription = striker.OnDeadEvent.Subscribe(OnStrikerDead);
                deadEventDisposables.Add(subscription);
            }
        }

        void DisposeDeadEventSubscriptions() {
            foreach (var subscription in deadEventDisposables) {
                subscription.Dispose();
            }
            deadEventDisposables.Clear();
        }

        RoundResult BuildRoundResult(int roundNumber, CorePlayerId deadPlayerId) {
            var strikers = strikerRegistry.GetAllStrikers().ToList();
            var deadHub = strikers.FirstOrDefault(x => x.PlayerId == deadPlayerId.value);
            var aliveRankings = strikers
                .Where(x => x.PlayerId != deadPlayerId.value)
                .OrderByDescending(x => x.HitPoint)
                .ToList();

            var rankings = new List<PlayerRoundRank>(strikers.Count);
            for (int i = 0; i < aliveRankings.Count; i++) {
                rankings.Add(new PlayerRoundRank(new CorePlayerId(aliveRankings[i].PlayerId), i + 1));
            }

            if (deadHub != null) {
                rankings.Add(new PlayerRoundRank(new CorePlayerId(deadHub.PlayerId), strikers.Count));
            }

            return new RoundResult(roundNumber, rankings);
        }
    }
}