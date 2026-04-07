using System;
using System.Threading.Tasks;
using R3;
using UnityEngine;
using CorePlayerId = App.PlayerId;

namespace Alice {
    public interface IBattlePresenter {
        Task PlayBattleOpeningAsync();
        Task PlayRoundStartAsync(int roundNumber);
        void EnterRoundPlayablePhase();
        Task PlayRoundEndTransitionAsync();
        Task PlayRoundResumeTransitionAsync();
        Task PlayBattleEndingAsync(CorePlayerId winner);
        Task PlayBattleFinishFadeInAsync();
        void PlayInpact(StrikerImpact command);
        void RequestAttention(int playerId, AttentionRequest request);
        Observable<Unit> OnPauseMenuRequested { get; }
        Observable<Unit> OnSuspendRequested { get; }
        Observable<Unit> OnResumeRequested { get; }
        void OpenSuspendMenu();
        void CloseSuspendMenu();
    }

    public class BattlePresenter : IBattlePresenter, IDisposable {
        const int MAX_SKIP_INPUT_PLAYER_SLOTS = 8;

        readonly IStrikerRegistry strikerRegistry;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IMusicPlayer musicPlayer;
        readonly BattlePresenterView battlePresenterView;
        readonly BattleSuspendMenuPresenter suspendMenuPresenter;

        CompositeDisposable skipInputSubscriptions = new();
        CompositeDisposable pauseMenuInputSubscriptions = new();
        CompositeDisposable suspendMenuSubscriptions = new();
        CompositeDisposable audioSubscriptions = new();
        readonly Subject<Unit> pauseMenuRequestedSubject = new();
        readonly Subject<Unit> suspendRequestedSubject = new();
        readonly Subject<Unit> resumeRequestedSubject = new();
        bool isCinematicSkipEnabled;

        public Observable<Unit> OnPauseMenuRequested => pauseMenuRequestedSubject;
        public Observable<Unit> OnSuspendRequested => suspendRequestedSubject;
        public Observable<Unit> OnResumeRequested => resumeRequestedSubject;

        public BattlePresenter(IStrikerRegistry strikerRegistry, IGamePadRegistry gamePadRegistry, IMusicPlayer musicPlayer, BattlePresenterView battlePresenterView, BattleSuspendMenuPresenter suspendMenuPresenter) {
            this.strikerRegistry = strikerRegistry;
            this.gamePadRegistry = gamePadRegistry;
            this.musicPlayer = musicPlayer;
            this.battlePresenterView = battlePresenterView;
            this.suspendMenuPresenter = suspendMenuPresenter;
            EnsureStageCameraConfigured();
            SubscribeSkipInput();
            SubscribePauseMenuInput();
            SubscribeSuspendMenuEvents();
            SubscribeAudioEvents();
            battlePresenterView.SetBattleUiHiddenAboveImmediately();
            CloseSuspendMenu();
        }

        public void Dispose() {
            skipInputSubscriptions.Dispose();
            pauseMenuInputSubscriptions.Dispose();
            suspendMenuSubscriptions.Dispose();
            audioSubscriptions.Dispose();
            pauseMenuRequestedSubject.Dispose();
            suspendRequestedSubject.Dispose();
            resumeRequestedSubject.Dispose();
            suspendMenuPresenter.Dispose();
        }

        void EnsureStageCameraConfigured() {
            battlePresenterView.StageCamera.SetPlayerCenterPositionResolver(GetPlayerCenterPosition);
            battlePresenterView.StageCamera.SetIntroPoseRequester(RequestIntroPose);
            battlePresenterView.StageCamera.SetVictoryPoseRequester(RequestVictoryPose);
        }

        Vector3 GetPlayerCenterPosition(int playerId) {
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                if (striker.PlayerId.CurrentValue == playerId) {
                    return striker.CenterPosition.CurrentValue;
                }
            }

            throw new InvalidOperationException($"Striker not found. playerId={playerId}");
        }

        public async Task PlayBattleOpeningAsync() {
            EnsureStageCameraConfigured();
            isCinematicSkipEnabled = true;
            try {
                await Task.WhenAll(
                    battlePresenterView.StageCamera.PresentIntroAsync(),
                    battlePresenterView.FadePresenter.PresentFadeOutAsync());
            }
            finally {
                isCinematicSkipEnabled = false;
            }

            await battlePresenterView.SlideBattleUiInAsync();
            await battlePresenterView.WaitAfterSlideBattleUiInAsync();
        }

        public async Task PlayRoundStartAsync(int roundNumber) {
            await battlePresenterView.RoundStartPresenter.PresentRoundStartAsync(roundNumber);
        }

        public void EnterRoundPlayablePhase() {
            EnsureStageCameraConfigured();
            battlePresenterView.StageCamera.PresentRoundPlayableStart();
        }

        public async Task PlayRoundEndTransitionAsync() {
            EnsureStageCameraConfigured();
            battlePresenterView.StageCamera.PresentRoundFinish();
            await battlePresenterView.FadePresenter.PresentFadeInAsync();
        }

        public async Task PlayRoundResumeTransitionAsync() {
            EnsureStageCameraConfigured();
            battlePresenterView.StageCamera.ResetRoundCamera();
            await battlePresenterView.FadePresenter.PresentFadeOutAsync();
        }

        public async Task PlayBattleEndingAsync(CorePlayerId winner) {
            EnsureStageCameraConfigured();
            battlePresenterView.StageCamera.PresentBattleFinish();
            await battlePresenterView.ResultTextPresenter.PresentBattleFinishAsync();
            await battlePresenterView.SlideBattleUiOutAsync();

            isCinematicSkipEnabled = true;
            try {
                await battlePresenterView.StageCamera.PresentOutroAsync(winner);
            }
            finally {
                isCinematicSkipEnabled = false;
            }
        }

        public async Task PlayBattleFinishFadeInAsync() {
            await battlePresenterView.FadePresenter.PresentFadeInAsync();
        }

        public void PlayInpact(StrikerImpact command) {
            battlePresenterView.StageCamera.RequestShake(command);
        }

        public void RequestAttention(int playerId, AttentionRequest request) {
            battlePresenterView.StageCamera.RequestAttention(playerId, request.DurationSeconds);
        }

        public void OpenSuspendMenu() {
            suspendMenuPresenter.Show();
        }

        public void CloseSuspendMenu() {
            suspendMenuPresenter.Hide();
        }

        void RequestIntroPose(int playerId) {
            var target = strikerRegistry.Get(playerId);
            if (target.TryGetValue(out var strikerHub)) {
                strikerHub.IntroPose();
            }
        }

        void RequestVictoryPose(int winnerPlayerId) {
            var winner = strikerRegistry.Get(winnerPlayerId);
            if (winner.TryGetValue(out var strikerHub)) {
                strikerHub.VictoryPose();
            }
        }

        void SubscribeSkipInput() {
            skipInputSubscriptions.Dispose();
            skipInputSubscriptions = new CompositeDisposable();

            for (int playerId = 0; playerId < MAX_SKIP_INPUT_PLAYER_SLOTS; playerId++) {
                SubscribeSkipForPlayer(playerId, skipInputSubscriptions);
            }
        }

        void SubscribeSkipForPlayer(int playerId, CompositeDisposable subscriptions) {
            var playerGamePad = gamePadRegistry.Get(playerId);
            playerGamePad.OnButtonDown
                .Where(button => button == GamePadButton.East)
                .Subscribe(_ => {
                    if (!isCinematicSkipEnabled) {
                        return;
                    }

                    battlePresenterView.StageCamera.RequestSequenceSkip();
                })
                .AddTo(subscriptions);
        }

        void SubscribePauseMenuInput() {
            pauseMenuInputSubscriptions.Dispose();
            pauseMenuInputSubscriptions = new CompositeDisposable();

            for (int playerId = 0; playerId < MAX_SKIP_INPUT_PLAYER_SLOTS; playerId++) {
                SubscribePauseMenuRequestForPlayer(playerId, pauseMenuInputSubscriptions);
            }
        }

        void SubscribePauseMenuRequestForPlayer(int playerId, CompositeDisposable subscriptions) {
            var playerGamePad = gamePadRegistry.Get(playerId);
            playerGamePad.OnButtonDown
                .Where(button => button == GamePadButton.Select)
                .Subscribe(_ => pauseMenuRequestedSubject.OnNext(Unit.Default))
                .AddTo(subscriptions);
        }

        void SubscribeSuspendMenuEvents() {
            suspendMenuSubscriptions.Dispose();
            suspendMenuSubscriptions = new CompositeDisposable();

            suspendMenuPresenter.OnSuspendRequested
                .Subscribe(_ => suspendRequestedSubject.OnNext(Unit.Default))
                .AddTo(suspendMenuSubscriptions);

            suspendMenuPresenter.OnResumeRequested
                .Subscribe(_ => resumeRequestedSubject.OnNext(Unit.Default))
                .AddTo(suspendMenuSubscriptions);
        }

        void SubscribeAudioEvents() {
            audioSubscriptions.Dispose();
            audioSubscriptions = new CompositeDisposable();

            musicPlayer.OnBeatTiming
                .Subscribe(_ => {
                    if(battlePresenterView.BeatSound) AudioSource.PlayClipAtPoint(battlePresenterView.BeatSound, Vector3.zero);
                })
                .AddTo(audioSubscriptions);
        }
    }
}