using System;
using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;
using CorePlayerId = App.PlayerId;

namespace Alice {
    public interface IBattlePresenter {
        Task PlayBattleOpeningAsync();
        Task PlayRoundStartAsync(int roundNumber);
        void EnterRoundPlayablePhase();
        Task PlayRoundEndTransitionAsync();
        Task PlayRoundResumeTransitionAsync();
        Task PlayBattleEndingAsync(CorePlayerId winner);
        void PlayInpact(StrikerImpact command);
        void RequestAttention(int playerId, AttentionRequest request);
        Observable<Unit> OnPauseMenuRequested { get; }
        Observable<Unit> OnSuspendRequested { get; }
        Observable<Unit> OnResumeRequested { get; }
        void OpenSuspendMenu();
        void CloseSuspendMenu();
    }

    public class BattlePresenter : MonoBehaviour, IBattlePresenter {
        const int MAX_SKIP_INPUT_PLAYER_SLOTS = 8;

        [Inject] IStrikerRegistry strikerRegistry;
        [Inject] IGamePadRegistry gamePadRegistry;
        [Inject] IMusicPlayer musicPlayer;

        [SerializeField] StageCamera stageCamera;
        [SerializeField] BattleRoundStartView roundStartPresenter;
        [SerializeField] BattleResultTextView resultTextPresenter;
        [SerializeField] BattleFadeView fadePresenter;
        [SerializeField] BattleSuspendMenuPresenter suspendMenuPresenter;
        [SerializeField] AudioClip beatSound;

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

        void Awake() {
            EnsureStageCameraConfigured();
        }

        void Start() {
            SubscribeSkipInput();
            SubscribePauseMenuInput();
            SubscribeSuspendMenuEvents();
            SubscribeAudioEvents();
            CloseSuspendMenu();
        }

        void OnDestroy() {
            skipInputSubscriptions.Dispose();
            pauseMenuInputSubscriptions.Dispose();
            suspendMenuSubscriptions.Dispose();
            audioSubscriptions.Dispose();
            pauseMenuRequestedSubject.Dispose();
            suspendRequestedSubject.Dispose();
            resumeRequestedSubject.Dispose();
        }

        void EnsureStageCameraConfigured() {
            stageCamera.SetPlayerCenterPositionResolver(GetPlayerCenterPosition);
            stageCamera.SetIntroPoseRequester(RequestIntroPose);
            stageCamera.SetVictoryPoseRequester(RequestVictoryPose);
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
                    stageCamera.PresentIntroAsync(),
                    fadePresenter.PresentFadeOutAsync());
            }
            finally {
                isCinematicSkipEnabled = false;
            }
        }

        public async Task PlayRoundStartAsync(int roundNumber) {
            await roundStartPresenter.PresentRoundStartAsync(roundNumber);
        }

        public void EnterRoundPlayablePhase() {
            EnsureStageCameraConfigured();
            stageCamera.PresentRoundPlayableStart();
        }

        public async Task PlayRoundEndTransitionAsync() {
            EnsureStageCameraConfigured();
            stageCamera.PresentRoundFinish();
            await fadePresenter.PresentFadeInAsync();
        }

        public async Task PlayRoundResumeTransitionAsync() {
            EnsureStageCameraConfigured();
            stageCamera.ResetRoundCamera();
            await fadePresenter.PresentFadeOutAsync();
        }

        public async Task PlayBattleEndingAsync(CorePlayerId winner) {
            EnsureStageCameraConfigured();
            stageCamera.PresentBattleFinish();
            await resultTextPresenter.PresentBattleFinishAsync();

            isCinematicSkipEnabled = true;
            try {
                await Task.WhenAll(
                    stageCamera.PresentOutroAsync(winner),
                    resultTextPresenter.PresentOutroAsync());
            }
            finally {
                isCinematicSkipEnabled = false;
            }

            await fadePresenter.PresentFadeInAsync();
        }

        public void PlayInpact(StrikerImpact command) {
            stageCamera.RequestShake(command);
        }

        public void RequestAttention(int playerId, AttentionRequest request) {
            stageCamera.RequestAttention(playerId, request.DurationSeconds);
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
                .Where(button => button == GamePadButton.Left)
                .Subscribe(_ => {
                    if (!isCinematicSkipEnabled) {
                        return;
                    }

                    stageCamera.RequestSequenceSkip();
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
                .Subscribe(_ => AudioSource.PlayClipAtPoint(beatSound, Vector3.zero))
                .AddTo(audioSubscriptions);
        }
    }
}