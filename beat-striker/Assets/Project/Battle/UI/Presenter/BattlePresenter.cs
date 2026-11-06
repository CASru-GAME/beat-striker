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
        Task PresentTutorialRoundWinAsync(int winnerPlayerId, int winnerRoundWinCount);
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
        bool IsAttentionPresentationActive { get; }
        void OpenSuspendMenu();
        void CloseSuspendMenu();
    }

    public class BattlePresenter : IBattlePresenter, IDisposable {
        const int MAX_SKIP_INPUT_PLAYER_SLOTS = 8;
        const int BATTLE_PLAYER_COUNT = 2;

        readonly IStrikerRegistry strikerRegistry;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IReplaySetting replaySetting;
        readonly PlayerSession playerSession;
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
        readonly CompositeDisposable specialCommandSubscriptions = new();
        readonly Dictionary<int, IDisposable> specialStateSubscriptions = new();
        readonly Subject<Unit> pauseMenuRequestedSubject = new();
        readonly Subject<Unit> suspendRequestedSubject = new();
        readonly Subject<Unit> resumeRequestedSubject = new();
        bool isCinematicSkipEnabled;
        string pendingAttentionTechniqueText = string.Empty;
        int attentionRequestSequence;
        bool suppressAttentionTextForCurrentRequest;
        bool isDisposed;
        int totalBeatCount;
        readonly Dictionary<int, int> firstEarlySuccessPlayerByBeat = new();
        readonly HashSet<int> playedSuccessSoundBeatIndexes = new();

        public Observable<Unit> OnPauseMenuRequested => pauseMenuRequestedSubject;
        public Observable<Unit> OnSuspendRequested => suspendRequestedSubject;
        public Observable<Unit> OnResumeRequested => resumeRequestedSubject;
        public Observable<bool> OnAttentionActiveStateChanged => battlePresenterView.StageCamera.OnAttentionActiveStateChanged;
        public bool IsAttentionPresentationActive => battlePresenterView.StageCamera.IsAttentionPresentationActive;

        [Inject]
        public BattlePresenter(IStrikerRegistry strikerRegistry, IGamePadRegistry gamePadRegistry, IReplaySetting replaySetting, IMusicPlayer musicPlayer, IBeatjudge beatJudge, IAISetting aiSetting, IBattleOpeningBgmPlayer battleOpeningBgmPlayer, BattlePresenterView battlePresenterView, BattleSuspendMenuPresenter suspendMenuPresenter, IAppStrikerRegistry appStrikerRegistry, PlayerSession playerSession) {
            this.strikerRegistry = strikerRegistry;
            this.appStrikerRegistry = appStrikerRegistry;
            this.gamePadRegistry = gamePadRegistry;
            this.replaySetting = replaySetting;
            this.playerSession = playerSession;
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
            SubscribeSpecialCommandEvents();
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
            specialCommandSubscriptions.Dispose();
            foreach (var subscription in specialStateSubscriptions.Values) subscription.Dispose();
            specialStateSubscriptions.Clear();
            battlePresenterView.HideSpecialCommand();
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
            battlePresenterView.HideSpecialCommand();
            if (aiSetting.IsInfiniteRoundMode || !continueBattle) {
                return;
            }

            var winnerRoundWinIndex = Mathf.Max(0, winnerRoundWinCount - 1);
            battlePresenterView.PlayRoundWinTrophySound();
            _ = battlePresenterView.PlayRoundWinTrophyAsync(winnerPlayerId, winnerRoundWinIndex);
        }

        public Task PresentTutorialRoundWinAsync(int winnerPlayerId, int winnerRoundWinCount) {
            battlePresenterView.HideSpecialCommand();
            battlePresenterView.PlayRoundWinTrophySound();
            return battlePresenterView.PlayRoundWinTrophyAsync(winnerPlayerId, Mathf.Max(0, winnerRoundWinCount - 1));
        }

        public void HandleBattleStarted() {
            battlePresenterView.ResetRoundWinTrophies();
        }

        public async Task PlayRoundEndTransitionAsync() {
            battlePresenterView.HideSpecialCommand();
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
            battlePresenterView.HideSpecialCommand();
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
            battlePresenterView.StageCamera.RequestAttention(playerId, request.DurationSeconds, request.OnReturnCompleted, request.ZoomSmoothTime, request.OnReturnStarted, request.ZoomDistance, request.ResumeOnReturnStarted);
            _ = HideAttentionTextBeforeLooseAsync(sequence, request.DurationSeconds);
        }

        public void RequestPauseMenu() {
            pauseMenuRequestedSubject.OnNext(Unit.Default);
        }

        public void OpenSuspendMenu() {
            battlePresenterView.HideSpecialCommand();
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
                    if (!isCinematicSkipEnabled || !CanSkipCinematicFromPlayer(playerId)) {
                        return;
                    }

                    battlePresenterView.StageCamera.RequestSequenceSkip();
                })
                .AddTo(subscriptions);
        }

        bool CanSkipCinematicFromPlayer(int playerId) {
            if (!replaySetting.HasReplay) {
                return true;
            }

            return playerId >= BATTLE_PLAYER_COUNT;
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
                    if (firstEarlySuccessPlayerByBeat.Remove(signal.BeatIndex, out var earlyPlayerId)
                        && (!IsCpuControlled(earlyPlayerId) || IsCpuControlled(1 - earlyPlayerId))) {
                        PlaySuccessSoundForBeat(signal.BeatIndex);
                    }

                    var remainingBeatCount = totalBeatCount - (signal.BeatIndex + 1);
                    battlePresenterView.SetRemainingBeatCount(remainingBeatCount);
                })
                .AddTo(audioSubscriptions);

            musicPlayer.OnViewBeatTiming
                .Subscribe(_ => {
                    battlePresenterView.RequestViewBeatPulse();
                })
                .AddTo(audioSubscriptions);

            musicPlayer.OnPlaybackLooped
                .Subscribe(_ => ResetSuccessSounds())
                .AddTo(audioSubscriptions);

            musicPlayer.OnBeatTimelinePrepared
                .Subscribe(beatTimeline => {
                    totalBeatCount = beatTimeline?.Length ?? 0;
                    ResetSuccessSounds();
                    battlePresenterView.SetRemainingBeatCountVs();
                })
                .AddTo(audioSubscriptions);

            musicPlayer.OnPlaybackCompleted
                .Subscribe(_ => {
                    ResetSuccessSounds();
                    battlePresenterView.SetRemainingBeatCount(0);
                })
                .AddTo(audioSubscriptions);

            for (var playerId = 0; playerId < BATTLE_PLAYER_COUNT; playerId++) {
                var capturedPlayerId = playerId;
                var beatPlayer = beatJudge.GetBeatPlayer(capturedPlayerId);

                beatPlayer.OnBeatCommandRequested
                    .Subscribe(result => PlayJudgeSound(capturedPlayerId, result))
                    .AddTo(audioSubscriptions);
            }
        }

        void PlayJudgeSound(int playerId, IBeatPlayer.BeatResult result) {
            if (!result.IsSuccess || result.Zone == BeatJudgeZone.Miss) {
                battlePresenterView.PlayJudgeMissSound();
                return;
            }

            var beatIndex = result.BeatIndex;
            var timeline = musicPlayer.CurrentBeatTimeline;
            if (beatIndex < 0 || beatIndex >= timeline.Length || playedSuccessSoundBeatIndexes.Contains(beatIndex)) return;

            var playerIsCpu = IsCpuControlled(playerId);
            var opponentIsCpu = IsCpuControlled(1 - playerId);
            if (playerIsCpu != opponentIsCpu) {
                if (playerIsCpu) return;
            } else if (musicPlayer.CurrentPlaybackTime < timeline[beatIndex]) {
                if (!firstEarlySuccessPlayerByBeat.TryGetValue(beatIndex, out var firstPlayerId)) {
                    firstEarlySuccessPlayerByBeat.Add(beatIndex, playerId);
                    return;
                }
                if (firstPlayerId == playerId) return;
            }

            firstEarlySuccessPlayerByBeat.Remove(beatIndex);
            PlaySuccessSoundForBeat(beatIndex);
        }

        bool IsCpuControlled(int playerId) {
            if (replaySetting.HasReplay && playerId < playerSession.Participants.Count) {
                return playerSession.Participants[playerId].isCpu;
            }

            return gamePadRegistry.Get(playerId).IsCpuControlled;
        }

        void ResetSuccessSounds() {
            firstEarlySuccessPlayerByBeat.Clear();
            playedSuccessSoundBeatIndexes.Clear();
        }

        void PlaySuccessSoundForBeat(int beatIndex) {
            if (playedSuccessSoundBeatIndexes.Add(beatIndex)) {
                battlePresenterView.PlayJudgeSuccessSound();
            }
        }

        void SubscribeSpecialCommandEvents() {
            battlePresenterView.HideSpecialCommand();
            strikerRegistry.OnRegistered
                .Subscribe(registration => ObserveSpecialState(registration.PlayerId, registration.Hub))
                .AddTo(specialCommandSubscriptions);
            strikerRegistry.OnUnregistered
                .Subscribe(registration => {
                    if (specialStateSubscriptions.Remove(registration.PlayerId, out var subscription)) {
                        subscription.Dispose();
                    }
                })
                .AddTo(specialCommandSubscriptions);
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                ObserveSpecialState(striker.PlayerId.CurrentValue, striker);
            }
        }

        void ObserveSpecialState(int playerId, IStrikerHub striker) {
            if (specialStateSubscriptions.Remove(playerId, out var subscription)) subscription.Dispose();
            specialStateSubscriptions[playerId] = striker.CurrentStateCategory
                .Select(category => category == StrikerStateCategory.Special)
                .DistinctUntilChanged()
                .Skip(1)
                .Where(isSpecial => isSpecial)
                .Subscribe(_ => battlePresenterView.PlaySpecialCommand(playerId,
                    appStrikerRegistry.GetByStriker(striker.Striker.CurrentValue).Portrait));
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
