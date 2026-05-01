using System;

namespace Alice {
    public sealed class BattleFlowPauseHandler {
        readonly BattleFlowStateMachine stateMachine;
        readonly IBeatjudge beatJudge;
        readonly IMusicPlayer musicPlayer;
        readonly IBattleDeployer battleDeployer;
        readonly IBattlePresenter battlePresenter;
        readonly IBattlePlayerPresenter[] battlePlayerPresenters;
        readonly Action completeBattleByPendingMusicEndIfNeeded;

        public BattleFlowPauseHandler(BattleFlowStateMachine stateMachine, IBeatjudge beatJudge, IMusicPlayer musicPlayer, IBattleDeployer battleDeployer, IBattlePresenter battlePresenter, IBattlePlayerPresenter[] battlePlayerPresenters, Action completeBattleByPendingMusicEndIfNeeded) {
            this.stateMachine = stateMachine;
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
            this.battleDeployer = battleDeployer;
            this.battlePresenter = battlePresenter;
            this.battlePlayerPresenters = battlePlayerPresenters;
            this.completeBattleByPendingMusicEndIfNeeded = completeBattleByPendingMusicEndIfNeeded;
        }

        public void ApplySuspendMenuPause() {
            if (!stateMachine.CanPauseForSuspend) {
                return;
            }

            battlePresenter.OpenSuspendMenu();
            PauseRoundForSuspendMenu();
        }

        public void ApplySuspendMenuResume() {
            if (!stateMachine.CanResumeFromSuspend) {
                return;
            }

            if (!stateMachine.TryEnterPlaying(nameof(ApplySuspendMenuResume))) {
                return;
            }

            ResumeRoundRuntimeSystems(controlsMusic: true);
            battlePresenter.CloseSuspendMenu();
            completeBattleByPendingMusicEndIfNeeded();
        }

        public void HandleAttentionActiveStateChanged(bool isActive) {
            if (stateMachine.IsBattleEndingOrFinished || stateMachine.IsRoundResolving || stateMachine.IsSuspended || stateMachine.IsTutorialSuspended) {
                return;
            }

            if (isActive) {
                if (!stateMachine.CanPauseForAttention) {
                    return;
                }

                if (!stateMachine.TryBeginAttentionSuspend(nameof(HandleAttentionActiveStateChanged))) {
                    return;
                }

                PauseRoundRuntimeSystems(controlsMusic: false);
                return;
            }

            if (!stateMachine.CanResumeFromAttention) {
                return;
            }

            if (!stateMachine.TryEnterPlaying(nameof(HandleAttentionActiveStateChanged))) {
                return;
            }

            ResumeRoundRuntimeSystems(controlsMusic: false);
            completeBattleByPendingMusicEndIfNeeded();
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
            completeBattleByPendingMusicEndIfNeeded();
        }

        public void PauseRoundRuntimeSystems(bool controlsMusic) {
            beatJudge.Pause();
            if (controlsMusic) {
                musicPlayer.Pause();
            }
            battleDeployer.PauseRound();
        }

        public void ResumeRoundRuntimeSystems(bool controlsMusic) {
            beatJudge.Resume();
            if (controlsMusic) {
                musicPlayer.Resume();
            }
            battleDeployer.ResumeRound();
        }

        public void PresentRoundPlayableFinishToPlayers() {
            for (var i = 0; i < battlePlayerPresenters.Length; i++) {
                battlePlayerPresenters[i].PresentRoundPlayableFinish();
            }
        }

        public void PresentTutorialPausedToPlayers() {
            for (var i = 0; i < battlePlayerPresenters.Length; i++) {
                battlePlayerPresenters[i].PresentTutorialPause();
            }
        }

        public void PresentTutorialResumedToPlayers() {
            for (var i = 0; i < battlePlayerPresenters.Length; i++) {
                battlePlayerPresenters[i].PresentTutorialResume();
            }
        }

        void PauseRoundForSuspendMenu() {
            if (!stateMachine.TryBeginSuspend(nameof(PauseRoundForSuspendMenu))) {
                return;
            }

            PauseRoundRuntimeSystems(controlsMusic: true);
        }
    }
}
