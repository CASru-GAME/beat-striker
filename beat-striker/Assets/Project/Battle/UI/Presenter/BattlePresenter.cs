using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;
using CorePlayerId = App.PlayerId;

namespace Alice {
    public interface IBattlePresenter {
        Task PlayBattleOpeningAsync();
        Task PlayRoundStartAsync(int roundNumber);
        void HandleRoundResolved(int winnerPlayerId, int winnerRoundWinCount, bool continueBattle);
        void HandleBattleStarted();
        void EnterRoundPlayablePhase();
        Task PlayRoundEndTransitionAsync();
        Task PlayRoundResumeTransitionAsync();
        Task PlayBattleEndingAsync(CorePlayerId winner);
        Task PlayBattleFinishFadeOutAsync();
        Task PlayBattleFinishFadeInAsync();
        void PlayInpact(StrikerImpact command);
        void RequestAttention(int playerId, AttentionRequest request);
        void RequestPauseMenu();
        Observable<Unit> OnPauseMenuRequested { get; }
        Observable<Unit> OnSuspendRequested { get; }
        Observable<Unit> OnResumeRequested { get; }
        Observable<bool> OnAttentionActiveStateChanged { get; }
        void OpenSuspendMenu();
        void CloseSuspendMenu();
    }

    public class BattlePresenter : IBattlePresenter, IDisposable {
        const int MAX_SKIP_INPUT_PLAYER_SLOTS = 8;
        const int BATTLE_PLAYER_COUNT = 2;

        readonly IStrikerRegistry strikerRegistry;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IMusicPlayer musicPlayer;
        readonly IBeatjudge beatJudge;
        readonly IAISetting aiSetting;
        readonly IBattleOpeningBgmPlayer battleOpeningBgmPlayer;
        readonly BattlePresenterView battlePresenterView;
        readonly BattleSuspendMenuPresenter suspendMenuPresenter;

        CompositeDisposable skipInputSubscriptions = new();
        CompositeDisposable pauseMenuInputSubscriptions = new();
        CompositeDisposable suspendMenuSubscriptions = new();
        CompositeDisposable audioSubscriptions = new();
        CompositeDisposable attentionTextSubscriptions = new();
        readonly Subject<Unit> pauseMenuRequestedSubject = new();
        readonly Subject<Unit> suspendRequestedSubject = new();
        readonly Subject<Unit> resumeRequestedSubject = new();
        bool isCinematicSkipEnabled;
        string pendingAttentionTechniqueText = string.Empty;
        int attentionRequestSequence;
        bool suppressAttentionTextForCurrentRequest;
        bool isDisposed;
        int totalBeatCount;
        int currentExcellentWindowBeatIndex = -1;
        readonly HashSet<int> pendingSuccessSoundBeatIndexes = new();
        readonly HashSet<int> playedSuccessSoundBeatIndexes = new();

        public Observable<Unit> OnPauseMenuRequested => pauseMenuRequestedSubject;
        public Observable<Unit> OnSuspendRequested => suspendRequestedSubject;
        public Observable<Unit> OnResumeRequested => resumeRequestedSubject;
        public Observable<bool> OnAttentionActiveStateChanged => battlePresenterView.StageCamera.OnAttentionActiveStateChanged;

        [Inject]
        public BattlePresenter(IStrikerRegistry strikerRegistry, IGamePadRegistry gamePadRegistry, IMusicPlayer musicPlayer, IBeatjudge beatJudge, IAISetting aiSetting, IBattleOpeningBgmPlayer battleOpeningBgmPlayer, BattlePresenterView battlePresenterView, BattleSuspendMenuPresenter suspendMenuPresenter) {
            this.strikerRegistry = strikerRegistry;
            this.gamePadRegistry = gamePadRegistry;
            this.musicPlayer = musicPlayer;
            this.beatJudge = beatJudge;
            this.aiSetting = aiSetting;
            this.battleOpeningBgmPlayer = battleOpeningBgmPlayer;
            this.battlePresenterView = battlePresenterView;
            this.suspendMenuPresenter = suspendMenuPresenter;
            EnsureStageCameraConfigured();
            SubscribeSkipInput();
            SubscribePauseMenuInput();
            SubscribeSuspendMenuEvents();
            SubscribeAudioEvents();
            SubscribeAttentionTextEvents();
            battlePresenterView.SetBattleUiHiddenAboveImmediately();
            CloseSuspendMenu();
            battlePresenterView.AttentionTextView.HideImmediately();
            battlePresenterView.SetRemainingBeatCountVs();
            battlePresenterView.ResetRoundWinTrophies();
        }

        public void Dispose() {
            isDisposed = true;
            battleOpeningBgmPlayer.Stop();
            battleOpeningBgmPlayer.StopBattleFinish();
            skipInputSubscriptions.Dispose();
            pauseMenuInputSubscriptions.Dispose();
            suspendMenuSubscriptions.Dispose();
            audioSubscriptions.Dispose();
            attentionTextSubscriptions.Dispose();
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
            battleOpeningBgmPlayer.Play();
            var introTask = battlePresenterView.StageCamera.PresentIntroAsync();
            var fadeTask = battlePresenterView.FadePresenter.PresentFadeOutAsync();
            try {
                await Task.WhenAll(
                    StopOpeningBgmAfterIntroAsync(introTask),
                    fadeTask);
            }
            finally {
                battleOpeningBgmPlayer.Stop();
                isCinematicSkipEnabled = false;
            }

            SetAllStrikersDefault();

            await battlePresenterView.SlideBattleUiInAsync();
            await battlePresenterView.WaitAfterSlideBattleUiInAsync();
        }

        async Task StopOpeningBgmAfterIntroAsync(Task introTask) {
            try {
                await introTask;
            }
            finally {
                battleOpeningBgmPlayer.Stop();
            }
        }

        public async Task PlayRoundStartAsync(int roundNumber) {
            await battlePresenterView.RoundStartPresenter.PresentRoundStartAsync(roundNumber);
        }

        public void EnterRoundPlayablePhase() {
            EnsureStageCameraConfigured();
            battlePresenterView.StageCamera.PresentRoundPlayableStart();
        }

        public void HandleRoundResolved(int winnerPlayerId, int winnerRoundWinCount, bool continueBattle) {
            if (aiSetting.IsInfiniteRoundMode || !continueBattle) {
                return;
            }

            var winnerRoundWinIndex = Mathf.Max(0, winnerRoundWinCount - 1);
            battlePresenterView.PlayRoundWinTrophySound();
            _ = battlePresenterView.PlayRoundWinTrophyAsync(winnerPlayerId, winnerRoundWinIndex);
        }

        public void HandleBattleStarted() {
            battlePresenterView.ResetRoundWinTrophies();
        }

        public async Task PlayRoundEndTransitionAsync() {
            EnsureStageCameraConfigured();
            battlePresenterView.StageCamera.PresentRoundFinish();
            await battlePresenterView.WaitBeforeRoundEndFadeInAsync();
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
            await Task.WhenAll(
                battlePresenterView.SlideBattleUiOutAsync(),
                battlePresenterView.HideRoundWinTrophiesToBottomAsync());

            isCinematicSkipEnabled = true;
            try {
                battleOpeningBgmPlayer.PlayBattleFinish();
                await battlePresenterView.StageCamera.PresentOutroAsync(winner);
            }
            finally {
                isCinematicSkipEnabled = false;
            }
        }

        public async Task PlayBattleFinishFadeOutAsync() {
            EnsureStageCameraConfigured();
            battlePresenterView.StageCamera.PresentBattleFinish();
            await battlePresenterView.FadePresenter.PresentFadeOutAsync();
        }

        public async Task PlayBattleFinishFadeInAsync() {
            battlePresenterView.StageCamera.PresentBattleFinish();
            await battlePresenterView.FadePresenter.PresentFadeInAsync();
        }

        public void PlayInpact(StrikerImpact command) {
            battlePresenterView.StageCamera.RequestShake(command);
        }

        public void RequestAttention(int playerId, AttentionRequest request) {
            pendingAttentionTechniqueText = request.TechniqueText;
            suppressAttentionTextForCurrentRequest = false;
            attentionRequestSequence += 1;
            var sequence = attentionRequestSequence;
            battlePresenterView.AttentionTextView.Hide();
            battlePresenterView.StageCamera.RequestAttention(playerId, request.DurationSeconds);
            _ = HideAttentionTextBeforeLooseAsync(sequence, request.DurationSeconds);
        }

        public void RequestPauseMenu() {
            pauseMenuRequestedSubject.OnNext(Unit.Default);
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

        void SetAllStrikersDefault() {
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                striker.Default();
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
                .Subscribe(signal => {
                    if(battlePresenterView.BeatSound) battlePresenterView.BeatSound.PlayAtApp(Vector3.zero);

                    var remainingBeatCount = totalBeatCount - (signal.BeatIndex + 1);
                    battlePresenterView.SetRemainingBeatCount(remainingBeatCount);
                })
                .AddTo(audioSubscriptions);

            musicPlayer.OnViewBeatTiming
                .Subscribe(_ => {
                    battlePresenterView.RequestViewBeatPulse();
                })
                .AddTo(audioSubscriptions);

            musicPlayer.OnExcellentZoneEntered
                .Subscribe(signal => {
                    if (currentExcellentWindowBeatIndex >= 0 && signal.BeatIndex < currentExcellentWindowBeatIndex) {
                        pendingSuccessSoundBeatIndexes.Clear();
                        playedSuccessSoundBeatIndexes.Clear();
                    }

                    currentExcellentWindowBeatIndex = signal.BeatIndex;
                    PlayPendingSuccessSoundIfNeeded(signal.BeatIndex);
                })
                .AddTo(audioSubscriptions);

            musicPlayer.OnBeatTimelinePrepared
                .Subscribe(beatTimeline => {
                    totalBeatCount = beatTimeline?.Length ?? 0;
                    currentExcellentWindowBeatIndex = -1;
                    pendingSuccessSoundBeatIndexes.Clear();
                    playedSuccessSoundBeatIndexes.Clear();
                    battlePresenterView.SetRemainingBeatCountVs();
                })
                .AddTo(audioSubscriptions);

            musicPlayer.OnPlaybackCompleted
                .Subscribe(_ => {
                    currentExcellentWindowBeatIndex = -1;
                    pendingSuccessSoundBeatIndexes.Clear();
                    playedSuccessSoundBeatIndexes.Clear();
                    battlePresenterView.SetRemainingBeatCount(0);
                })
                .AddTo(audioSubscriptions);

            for (var playerId = 0; playerId < BATTLE_PLAYER_COUNT; playerId++) {
                var beatPlayer = beatJudge.GetBeatPlayer(playerId);

                beatPlayer.OnBeatCommandRequested
                    .Subscribe(result => {
                        if (!result.IsSuccess || result.Zone == BeatJudgeZone.Miss) {
                            battlePresenterView.PlayJudgeMissSound();
                            return;
                        }

                        var judgeResult = musicPlayer.JudgeTiming(result.Time);
                        if (result.Zone != BeatJudgeZone.Excellent) {
                            if (result.Zone == BeatJudgeZone.Good) {
                                pendingSuccessSoundBeatIndexes.Add(judgeResult.BeatIndex);
                                PlayPendingSuccessSoundIfNeeded(currentExcellentWindowBeatIndex);
                            }

                            return;
                        }

                        pendingSuccessSoundBeatIndexes.Remove(judgeResult.BeatIndex);
                        PlaySuccessSoundForBeat(judgeResult.BeatIndex);
                    })
                    .AddTo(audioSubscriptions);

            }
        }

        void PlayPendingSuccessSoundIfNeeded(int beatIndex) {
            if (beatIndex < 0 || !pendingSuccessSoundBeatIndexes.Remove(beatIndex)) {
                return;
            }

            PlaySuccessSoundForBeat(beatIndex);
        }

        void PlaySuccessSoundForBeat(int beatIndex) {
            if (beatIndex < 0 || !playedSuccessSoundBeatIndexes.Add(beatIndex)) {
                return;
            }

            battlePresenterView.PlayJudgeSuccessSound();
        }

        void SubscribeAttentionTextEvents() {
            attentionTextSubscriptions.Dispose();
            attentionTextSubscriptions = new CompositeDisposable();

            battlePresenterView.StageCamera.OnAttentionActiveStateChanged
                .Subscribe(isActive => {
                    if (!isActive) {
                        battlePresenterView.AttentionTextView.Hide();
                    }
                })
                .AddTo(attentionTextSubscriptions);

            battlePresenterView.StageCamera.OnAttentionFocusStateChanged
                .Subscribe(isFocused => {
                    if (!isFocused) {
                        battlePresenterView.AttentionTextView.Hide();
                        return;
                    }

                    if (suppressAttentionTextForCurrentRequest) {
                        return;
                    }

                    battlePresenterView.AttentionTextView.Show(pendingAttentionTechniqueText);
                })
                .AddTo(attentionTextSubscriptions);
        }

        async Task HideAttentionTextBeforeLooseAsync(int sequence, float requestDurationSeconds) {
            var hideLeadSeconds = Mathf.Max(0f, battlePresenterView.AttentionTextView.HideDelay);
            var hideDelaySeconds = Mathf.Max(0f, requestDurationSeconds - hideLeadSeconds);

            if (hideDelaySeconds <= 0f) {
                if (isDisposed || sequence != attentionRequestSequence) {
                    return;
                }

                suppressAttentionTextForCurrentRequest = true;
                battlePresenterView.AttentionTextView.Hide();
                return;
            }

            await DelayAsync(hideDelaySeconds);
            if (isDisposed || sequence != attentionRequestSequence) {
                return;
            }

            suppressAttentionTextForCurrentRequest = true;
            battlePresenterView.AttentionTextView.Hide();
        }

        static Task DelayAsync(float seconds) {
            if (seconds <= 0f) {
                return Task.CompletedTask;
            }

            var completionSource = new TaskCompletionSource<bool>();
            LeanTween.delayedCall(seconds, () => completionSource.TrySetResult(true));
            return completionSource.Task;
        }
    }
}