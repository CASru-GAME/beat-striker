
using R3;
using System;
using App;
using UnityEngine;

namespace Alice {
    public interface IBattleFlow {
        Observable<int> RoundStarted { get; }
        Observable<Unit> BattleFinished { get; }
        Observable<Unit> OutroStarted { get; }

        void PrepareBattle();
        void StartBattle();
        void NotifyIntroAnimationFinished();
        void NotifyRoundStartAnimationFinished();
        void NotifyRoundFinishAnimationFinished();
        void NotifyOutroAnimationFinished();
        void NotifyPlayerDead(Core.App.Types.PlayerId playerId);
    }

    public class BattleFlow : IBattleFlow {
        readonly IBattleDeployer battleDeployer;
        readonly IMusicPlayer musicPlayer;
        int currentRound;
        bool battlePrepared;
        bool battleStarted;
        bool battleFinished;

        readonly Subject<int> roundStartedSubject = new();
        readonly Subject<Unit> battleFinishedSubject = new();
        readonly Subject<Unit> outroStartedSubject = new();

        public Observable<int> RoundStarted => roundStartedSubject;
        public Observable<Unit> BattleFinished => battleFinishedSubject;
        public Observable<Unit> OutroStarted => outroStartedSubject;

        public BattleFlow(IBattleDeployer battleDeployer, IMusicPlayer musicPlayer) {
            this.battleDeployer = battleDeployer;
            this.musicPlayer = musicPlayer;
            currentRound = 1;
            battlePrepared = false;
            battleStarted = false;
            battleFinished = false;
        }

        public void PrepareBattle() {
            if (battlePrepared) return;

            battlePrepared = true;
            battleDeployer.Deploy();
        }

        public void StartBattle() {
            if (battleStarted) return;

            PrepareBattle();
            battleStarted = true;
            Debug.Log("Battle Started".ToCyan());
            musicPlayer.Play();
            roundStartedSubject.OnNext(currentRound);
        }

        public void NotifyIntroAnimationFinished() {
            if (!battleStarted) {
                StartBattle();
            }
        }

        public void NotifyRoundStartAnimationFinished() {
        }

        public void NotifyRoundFinishAnimationFinished() {
            if (battleFinished) return;

            currentRound += 1;
            roundStartedSubject.OnNext(currentRound);
        }

        public void NotifyOutroAnimationFinished() {
        }

        public void NotifyPlayerDead(Core.App.Types.PlayerId _) {
            if (battleFinished) return;

            battleFinished = true;
            battleFinishedSubject.OnNext(Unit.Default);
            outroStartedSubject.OnNext(Unit.Default);
        }
    }
}