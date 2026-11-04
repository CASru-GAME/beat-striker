using System;
using System.Threading.Tasks;
using App;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public interface IBattleTutorialSignalEmitter {
        Observable<Unit> OnTutorialPauseRequested { get; }
        Observable<Unit> OnTutorialResumeRequested { get; }
        Observable<Unit> OnTutorialEndBattleToTitleRequested { get; }
    }

    public class BattleTutorialPresenter : IBattleTutorialSignalEmitter, IDisposable {
        const string LOG_PREFIX = "[BattleTutorialPresenter]";
        const int TUTORIAL_PLAYER_ID = 0;

        readonly IBeatjudge beatJudge;
        readonly IStrikerRegistry strikerRegistry;
        readonly IGamePadRegistry gamePadRegistry;
        readonly ITutorialSetting tutorialSetting;
        readonly IBattleJudge battleJudge;
        readonly IBattlePresenter battlePresenter;
        readonly BattleTutorialView tutorialView;
        readonly CompositeDisposable subscriptions = new();
        readonly Subject<Unit> tutorialPauseRequestedSubject = new();
        readonly Subject<Unit> tutorialResumeRequestedSubject = new();
        readonly Subject<Unit> tutorialEndBattleToTitleRequestedSubject = new();
        bool started;
        bool disposed;
        readonly TaskCompletionSource<bool> disposalCompletion = new();
        const int RHYTHM_REQUIRED_SUCCESSES = 3;

        public Observable<Unit> OnTutorialPauseRequested => tutorialPauseRequestedSubject;
        public Observable<Unit> OnTutorialResumeRequested => tutorialResumeRequestedSubject;
        public Observable<Unit> OnTutorialEndBattleToTitleRequested => tutorialEndBattleToTitleRequestedSubject;

        [Inject]
        public BattleTutorialPresenter(
            IBeatjudge beatJudge,
            IStrikerRegistry strikerRegistry,
            IGamePadRegistry gamePadRegistry,
            ITutorialSetting tutorialSetting,
            IAudioSetting audioSetting,
            IBattleJudge battleJudge,
            IBattlePresenter battlePresenter,
            BattleTutorialView tutorialView) {
            this.beatJudge = beatJudge;
            this.strikerRegistry = strikerRegistry;
            this.gamePadRegistry = gamePadRegistry;
            this.tutorialSetting = tutorialSetting;
            this.tutorialView = tutorialView;
            this.battleJudge = battleJudge;
            this.battlePresenter = battlePresenter;

            if (!tutorialSetting.IsTutorialBattleRequested) {
                tutorialView.HideAll();
                return;
            }

            tutorialView.SetNarrationInput(ResolveVoiceInput());
            Observable.EveryUpdate()
                .Select(_ => (Keyboard: gamePadRegistry.Get(TUTORIAL_PLAYER_ID).DeviceName == "Keybord",
                    East: gamePadRegistry.GetPhysicalFaceButton(GamePadButton.East)))
                .DistinctUntilChanged()
                .Subscribe(_ => tutorialView.SetNarrationInput(ResolveVoiceInput()))
                .AddTo(subscriptions);
            audioSetting.VolumeBalance
                .Subscribe(balance => tutorialView.SetNarrationVolume(balance.MasterVolume * balance.SeVolume))
                .AddTo(subscriptions);

            var beatPlayer = beatJudge.GetBeatPlayer(TUTORIAL_PLAYER_ID);

            beatPlayer.OnBeatPassed
                .Subscribe(_ => TryStartTutorialSequence())
                .AddTo(subscriptions);

            beatPlayer.OnBeatCommandExecuted
                .Subscribe(_event => {
                    TryStartTutorialSequence();
                })
                .AddTo(subscriptions);
        }

        void TryStartTutorialSequence() {
            if (started || !tutorialSetting.IsTutorialBattleRequested) {
                return;
            }

            started = true;
            _ = RunTutorialSequenceAsync();
        }

        TutorialVoiceInput ResolveVoiceInput() {
            var keyboard = gamePadRegistry.Get(TUTORIAL_PLAYER_ID).DeviceName == "Keybord";
            string ButtonClip(GamePadButton button) {
                var physical = gamePadRegistry.GetPhysicalFaceButton(button);
                return (keyboard, physical) switch {
                    (true, GamePadButton.North) => "key_i",
                    (true, GamePadButton.West) => "key_j",
                    (true, GamePadButton.South) => "key_k",
                    (true, GamePadButton.East) => "key_l",
                    (false, GamePadButton.North) => "button_x",
                    (false, GamePadButton.West) => "button_y",
                    (false, GamePadButton.South) => "button_b",
                    (false, GamePadButton.East) => "button_a",
                    _ => throw new ArgumentOutOfRangeException(nameof(button)),
                };
            }
            return new TutorialVoiceInput(
                ButtonClip(GamePadButton.East), ButtonClip(GamePadButton.South),
                ButtonClip(GamePadButton.West), ButtonClip(GamePadButton.North),
                keyboard ? "key_u" : "button_start",
                keyboard ? "walk_keyboard" : "walk_stick",
                keyboard ? "direction_keyboard" : "direction_stick",
                keyboard ? "I / J / K / L キー" : "A / B / X / Y ボタン",
                keyboard ? "W / A / S / D キーを押し続けたまま" : "左スティックを行きたい方向に倒したまま");
        }

        async Task RunTutorialSequenceAsync() {
            try {
                Debug.Log($"{LOG_PREFIX} RunTutorialSequenceAsync started");

                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                RequestTutorialPause();
                await AwaitWhileActiveAsync(tutorialView.ShowAsync(TutorialUiKey.Overview));
                await AwaitWhileActiveAsync(WaitForEastAsync());
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                tutorialView.HideAll();
                await AwaitWhileActiveAsync(RunDescriptionSequenceAsync(
                    TutorialUiKey.HpDescription,
                    TutorialUiKey.SpDescription));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                RequestTutorialResume();

                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.WalkDescription,
                    TutorialUiKey.WalkPractice,
                    WaitForWalkAsync));

                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.RhythmDescription,
                    TutorialUiKey.RhythmPractice,
                    WaitForRhythmAsync));

                await AwaitWhileActiveAsync(RunDescriptionSequenceAsync(TutorialUiKey.DirectionDescription));

                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.DashDescription,
                    TutorialUiKey.DashPractice,
                    () => WaitForStateCategoryAsync(StrikerStateCategory.Dash)));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.AttackDescription,
                    TutorialUiKey.AttackPractice,
                    () => WaitForStateCategoryAsync(StrikerStateCategory.Attack)));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.ChargeDescription,
                    TutorialUiKey.ChargePractice,
                    () => WaitForStateCategoryAsync(StrikerStateCategory.Charge)));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.ChargeAttackDescription,
                    TutorialUiKey.ChargeAttackPractice,
                    WaitForChargeAttackAsync));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.GuardDescription,
                    TutorialUiKey.GuardPractice,
                    () => WaitForStateCategoryAsync(StrikerStateCategory.Guard)));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                if (!TryFillSpecialPointMax()) {
                    tutorialView.HideAll();
                    return;
                }

                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.SpecialDescription,
                    TutorialUiKey.SpecialPractice,
                    WaitForSpecialCompletionAsync));

                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await AwaitWhileActiveAsync(RunDescriptionSequenceAsync(TutorialUiKey.TimerDescription, TutorialUiKey.VictoryDescription));
                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.ScoreDescription,
                    TutorialUiKey.DefeatPractice,
                    WaitForDefeatAsync));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await AwaitWhileActiveAsync(RunDescriptionSequenceAsync(TutorialUiKey.ScoreAwardDescription));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                RequestTutorialPause();
                await AwaitWhileActiveAsync(tutorialView.ShowAsync(TutorialUiKey.Final));
                await AwaitWhileActiveAsync(tutorialView.HideAfterFinalDelayAsync());
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                tutorialView.HideAll();
                tutorialSetting.ClearTutorialBattleRequest();
                tutorialEndBattleToTitleRequestedSubject.OnNext(Unit.Default);

                Debug.Log($"{LOG_PREFIX} RunTutorialSequenceAsync completed");
            }
            catch (OperationCanceledException) when (disposed) {
            }
            catch (Exception exception) {
                Debug.LogException(exception);
                tutorialView.HideAll();
                tutorialSetting.ClearTutorialBattleRequest();
            }
        }

        async Task RunActionStepAsync(
            TutorialUiKey descriptionKey,
            TutorialUiKey practiceKey,
            Func<Task> waitActionAsync) {
            if (disposed || !tutorialSetting.IsTutorialBattleRequested) {
                return;
            }

            RequestTutorialPause();
            await AwaitWhileActiveAsync(tutorialView.ShowAsync(descriptionKey));
            await AwaitWhileActiveAsync(WaitForEastAsync());
            if (!tutorialSetting.IsTutorialBattleRequested) {
                tutorialView.HideAll();
                return;
            }

            if (practiceKey == TutorialUiKey.RhythmPractice) {
                tutorialView.SetRhythmProgress(0, RHYTHM_REQUIRED_SUCCESSES);
            }
            await AwaitWhileActiveAsync(tutorialView.ShowAsync(practiceKey));
            // Subscribe to the requested action before input and simulation are resumed.
            var actionCompletion = waitActionAsync();
            RequestTutorialResume();
            await AwaitWhileActiveAsync(actionCompletion);
            if (disposed || !tutorialSetting.IsTutorialBattleRequested) {
                tutorialView.HideAll();
                return;
            }

            RequestTutorialPause();
            await AwaitWhileActiveAsync(tutorialView.HideAfterClearDelayAsync(practiceKey));
        }

        async Task RunDescriptionSequenceAsync(params TutorialUiKey[] descriptionKeys) {
            if (disposed || !tutorialSetting.IsTutorialBattleRequested) {
                return;
            }

            RequestTutorialPause();

            for (var i = 0; i < descriptionKeys.Length; i++) {
                await AwaitWhileActiveAsync(tutorialView.ShowAsync(descriptionKeys[i]));
                await AwaitWhileActiveAsync(WaitForEastAsync());

                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }
            }

            tutorialView.HideAll();
            RequestTutorialResume();
        }

        async Task AwaitWhileActiveAsync(Task task) {
            await Task.WhenAny(task, disposalCompletion.Task);
            if (disposed) throw new OperationCanceledException();
            await task;
            if (disposed) throw new OperationCanceledException();
        }

        void RequestTutorialPause() {
            tutorialPauseRequestedSubject.OnNext(Unit.Default);
        }

        void RequestTutorialResume() {
            tutorialResumeRequestedSubject.OnNext(Unit.Default);
        }

        Task WaitForEastAsync() {
            var tcs = new TaskCompletionSource<bool>();
            IDisposable subscription = null;
            subscription = gamePadRegistry.Get(TUTORIAL_PLAYER_ID).OnButtonDown
                .Where(button => button == GamePadButton.East)
                .Subscribe(_ => {
                    subscription.Dispose();
                    tcs.TrySetResult(true);
                });

            if (!tutorialSetting.IsTutorialBattleRequested) {
                subscription.Dispose();
                tcs.TrySetResult(true);
            }

            subscription.AddTo(subscriptions);
            return tcs.Task;
        }

        async Task WaitForRhythmAsync() {
            var beatPlayer = beatJudge.GetBeatPlayer(TUTORIAL_PLAYER_ID);
            var completion = new TaskCompletionSource<bool>();
            var successCount = 0;
            var previousBeatIndex = -1;
            using var practiceSubscriptions = new CompositeDisposable();

            void ResetProgress() {
                successCount = 0;
                previousBeatIndex = -1;
                tutorialView.SetRhythmProgress(0, RHYTHM_REQUIRED_SUCCESSES);
            }

            beatPlayer.OnBeatCommandRequested
                .Where(result => !result.IsSuccess)
                .Subscribe(_ => ResetProgress()).AddTo(practiceSubscriptions);
            beatPlayer.OnBeatPassed.Subscribe(_ => ResetProgress()).AddTo(practiceSubscriptions);
            beatPlayer.OnBeatCommandExecuted.Subscribe(result => {
                if (!result.IsSuccess || result.Button is not (GamePadButton.North or GamePadButton.East or GamePadButton.South or GamePadButton.West)) {
                    ResetProgress();
                    return;
                }
                successCount = successCount > 0 && result.BeatIndex == previousBeatIndex + 1 ? successCount + 1 : 1;
                previousBeatIndex = result.BeatIndex;
                tutorialView.SetRhythmProgress(successCount, RHYTHM_REQUIRED_SUCCESSES);
                if (successCount >= RHYTHM_REQUIRED_SUCCESSES) completion.TrySetResult(true);
            }).AddTo(practiceSubscriptions);
            Observable.EveryUpdate().Subscribe(_ => {
                if (disposed || !tutorialSetting.IsTutorialBattleRequested) completion.TrySetResult(false);
            }).AddTo(practiceSubscriptions);

            await AwaitWhileActiveAsync(completion.Task);
        }

        async Task WaitForSpecialCompletionAsync() {
            await AwaitWhileActiveAsync(WaitForStateCategoryAsync(StrikerStateCategory.Special));
            if (!TryResolveTutorialStriker(out var striker)) {
                throw new InvalidOperationException("Tutorial player is not deployed.");
            }

            var completion = new TaskCompletionSource<bool>();
            using var subscription = Observable.EveryUpdate().Subscribe(_ => {
                if (!tutorialSetting.IsTutorialBattleRequested ||
                    (striker.CurrentStateCategory.CurrentValue != StrikerStateCategory.Special &&
                     !battlePresenter.IsAttentionPresentationActive)) {
                    completion.TrySetResult(true);
                }
            });
            await AwaitWhileActiveAsync(completion.Task);
        }

        async Task WaitForDefeatAsync() {
            if (!strikerRegistry.Get(1).TryGetValue(out var opponent)) {
                throw new InvalidOperationException("Tutorial opponent is not deployed.");
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var deathSubscription = opponent.OnDead.Subscribe(_ => completion.TrySetResult(true));
            opponent.RestoreHitPoint();
            opponent.ConfigureSurvival(false, 0f);
            await AwaitWhileActiveAsync(completion.Task);
            if (!tutorialSetting.IsTutorialBattleRequested) return;

            RequestTutorialPause();
            var result = battleJudge.Judge(new RoundResult(1, new[] {
                new PlayerRoundRank(new PlayerId(TUTORIAL_PLAYER_ID), 1),
                new PlayerRoundRank(new PlayerId(1), 2),
            }));
            var winCount = result.RoundWins[new PlayerId(TUTORIAL_PLAYER_ID)];
            await AwaitWhileActiveAsync(battlePresenter.PresentTutorialRoundWinAsync(TUTORIAL_PLAYER_ID, winCount));
        }

        Task WaitForWalkAsync() {
            if (!TryResolveTutorialStriker(out var striker)) {
                return Task.CompletedTask;
            }

            var threshold = tutorialView.WalkVelocityThreshold;
            var requiredTravelDistance = tutorialView.WalkPracticeRequiredTravelDistance;
            var tcs = new TaskCompletionSource<bool>();
            var previousPosition = striker.Position.CurrentValue;
            var travelDistance = 0f;

            IDisposable subscription = null;
            subscription = Observable.EveryUpdate()
                .Subscribe(_ => {
                    if (!tutorialSetting.IsTutorialBattleRequested) {
                        subscription.Dispose();
                        tcs.TrySetResult(true);
                        return;
                    }

                    var currentPosition = striker.Position.CurrentValue;
                    var stepDistance = new Vector2(
                        currentPosition.x - previousPosition.x,
                        currentPosition.z - previousPosition.z).magnitude;
                    previousPosition = currentPosition;

                    var velocity = striker.Velocity.CurrentValue;
                    var horizontal = new Vector2(velocity.x, velocity.z).magnitude;
                    if (horizontal < threshold) {
                        return;
                    }

                    travelDistance += stepDistance;
                    if (travelDistance < requiredTravelDistance) {
                        return;
                    }

                    subscription.Dispose();
                    tcs.TrySetResult(true);
                });

            subscription.AddTo(subscriptions);
            return tcs.Task;
        }

        async Task WaitForChargeAttackAsync() {
            await AwaitWhileActiveAsync(WaitForStateCategoryAsync(StrikerStateCategory.Charge));
            await AwaitWhileActiveAsync(WaitForStateCategoryAsync(StrikerStateCategory.Attack));
        }

        Task WaitForStateCategoryAsync(StrikerStateCategory targetCategory) {
            if (!TryResolveTutorialStriker(out var striker)) {
                return Task.CompletedTask;
            }

            var hasLeftTargetState = striker.CurrentStateCategory.CurrentValue != targetCategory;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            IDisposable subscription = null;
            subscription = striker.CurrentStateCategory
                .Subscribe(category => {
                    if (!tutorialSetting.IsTutorialBattleRequested) {
                        subscription?.Dispose();
                        tcs.TrySetResult(true);
                        return;
                    }

                    if (category != targetCategory) {
                        hasLeftTargetState = true;
                        return;
                    }

                    if (!hasLeftTargetState) {
                        return;
                    }

                    subscription?.Dispose();
                    tcs.TrySetResult(true);
                });

            // ReactiveProperty emits synchronously during Subscribe, before assignment.
            if (tcs.Task.IsCompleted) subscription.Dispose();
            subscription.AddTo(subscriptions);
            return tcs.Task;
        }

        bool TryFillSpecialPointMax() {
            if (!TryResolveTutorialStriker(out var striker)) {
                return false;
            }

            striker.AddSpecialPoint(float.MaxValue);
            return true;
        }

        bool TryResolveTutorialStriker(out IStrikerHub striker) {
            striker = null;
            var option = strikerRegistry.Get(TUTORIAL_PLAYER_ID);
            if (!option.TryGetValue(out var resolvedStriker)) {
                return false;
            }

            striker = resolvedStriker;
            return true;
        }

        public void Dispose() {
            disposed = true;
            disposalCompletion.TrySetResult(true);
            subscriptions.Dispose();
            tutorialPauseRequestedSubject.Dispose();
            tutorialResumeRequestedSubject.Dispose();
            tutorialEndBattleToTitleRequestedSubject.Dispose();
            tutorialView.HideAll();
        }
    }
}
