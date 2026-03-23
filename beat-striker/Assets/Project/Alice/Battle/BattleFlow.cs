
using R3;
using System;
using System.Collections.Generic;
using App;
using Core.Striker;
using UnityEngine;
using VContainer;


namespace Alice {
    public interface IBattleFlow {
        void StartBattle();
    }

    public class BattleFlow : IBattleFlow {
        readonly IBattleDeployer battleDeployer;
        readonly IMusicPlayer musicPlayer;

        public BattleFlow(IBattleDeployer battleDeployer, IMusicPlayer musicPlayer) {
            this.battleDeployer = battleDeployer;
            this.musicPlayer = musicPlayer;
        }

        public void StartBattle() {
            Debug.Log("Battle Started".ToCyan());
            battleDeployer.Deploy();
            musicPlayer.Play();
        }
    }
}