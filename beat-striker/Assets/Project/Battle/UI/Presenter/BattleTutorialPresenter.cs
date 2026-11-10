using System;
using System.Linq;
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
        readonly IMusicPlayer musicPlayer;
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
        TutorialActionLesson currentLesson;
        int currentDescriptionPage;
        bool showingAfterPractice;
        TutorialActionStep currentPracticeStep;
        int currentPracticeCount;
        bool currentPracticeRetry;

        public Observable<Unit> OnTutorialPauseRequested => tutorialPauseRequestedSubject;
        public Observable<Unit> OnTutorialResumeRequested => tutorialResumeRequestedSubject;
        public Observable<Unit> OnTutorialEndBattleToTitleRequested => tutorialEndBattleToTitleRequestedSubject;

        [Inject]
        public BattleTutorialPresenter(
            IBeatjudge beatJudge,
            IMusicPlayer musicPlayer,
            IStrikerRegistry strikerRegistry,
            IGamePadRegistry gamePadRegistry,
            ITutorialSetting tutorialSetting,
            IAudioSetting audioSetting,
            IBattleJudge battleJudge,
            IBattlePresenter battlePresenter,
            BattleTutorialView tutorialView) {
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
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

            RefreshTutorialInput();
            Observable.EveryUpdate()
                .Select(_ => (Keyboard: gamePadRegistry.Get(TUTORIAL_PLAYER_ID).DeviceName == "Keybord",
                    East: gamePadRegistry.GetPhysicalFaceButton(GamePadButton.East),
                    FacingRight: !TryResolveTutorialStriker(out var striker) || striker.LookDirection.CurrentValue.x >= 0f))
                .DistinctUntilChanged()
                .Subscribe(_ => RefreshTutorialInput())
                .AddTo(subscriptions);
            audioSetting.VolumeBalance
                .Subscribe(balance => tutorialView.SetNarrationVolume(balance.MasterVolume * balance.SeVolume))
                .AddTo(subscriptions);

            musicPlayer.OnBeatTiming
                .Subscribe(_ => { TryStartTutorialSequence(); tutorialView.PulseRhythmGuide(); })
                .AddTo(subscriptions);
        }

        void TryStartTutorialSequence() {
            if (started || !tutorialSetting.IsTutorialBattleRequested) {
                return;
            }

            started = true;
            tutorialView.SetGuideBeatSeconds(musicPlayer.CurrentBeatTimeline[1] - musicPlayer.CurrentBeatTimeline[0]);
            _ = RunTutorialSequenceAsync();
        }

        TutorialVoiceInput ResolveVoiceInput() {
            var keyboard = gamePadRegistry.Get(TUTORIAL_PLAYER_ID).DeviceName == "Keybord";
            var facingRight = !TryResolveTutorialStriker(out var striker) || striker.LookDirection.CurrentValue.x >= 0f;
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
                keyboard ? "W / A / S / D キーを押し続けたまま" : "左スティックを行きたい方向に倒したまま") {
                Up = keyboard ? "lesson_input_up_keyboard" : "lesson_input_up_stick",
                Down = keyboard ? "lesson_input_down_keyboard" : "lesson_input_down_stick",
                Forward = keyboard ? (facingRight ? "lesson_input_forward_right" : "lesson_input_forward_left") : "lesson_input_forward_stick",
            };
        }

        void RefreshTutorialInput() {
            var input = ResolveVoiceInput();
            tutorialView.SetNarrationInput(input);
            tutorialView.SetInputLayout(gamePadRegistry.Get(TUTORIAL_PLAYER_ID).DeviceName == "Keybord",
                PhysicalControl(GamePadButton.East));
            if (currentLesson != null) {
                var pages = showingAfterPractice ? currentLesson.AfterPracticePages : currentLesson.Pages;
                tutorialView.SetCharacterLessonText(currentLesson.Title, pages[currentDescriptionPage].Text);
                RefreshPracticeInput();
            }
        }

        TutorialControl PhysicalControl(GamePadButton button) => gamePadRegistry.GetPhysicalFaceButton(button) switch {
            GamePadButton.North => TutorialControl.North, GamePadButton.South => TutorialControl.South,
            GamePadButton.West => TutorialControl.West, GamePadButton.East => TutorialControl.East,
            GamePadButton.Start => TutorialControl.Start, GamePadButton.Select => TutorialControl.Select,
            _ => TutorialControl.None,
        };

        void RefreshPracticeInput() {
            if (currentPracticeStep == null) return;
            var facing = !TryResolveTutorialStriker(out var striker) || striker.LookDirection.CurrentValue.x >= 0f ? 1f : -1f;
            TutorialInputCue Cue(TutorialActionStep step) => new(PhysicalControl(step.Command), step.CueDirection switch {
                TutorialDirection.Up => Vector2.up, TutorialDirection.Down => Vector2.down,
                TutorialDirection.Forward => Vector2.right * facing,
                TutorialDirection.ForwardUp => new Vector2(facing, 1f), _ => Vector2.zero,
            });
            var input = ResolveVoiceInput();
            tutorialView.SetCharacterPracticeStep(
                FormatLessonText(currentLesson.PracticeInstruction, input),
                currentPracticeCount, currentPracticeStep.RequiredCount, currentPracticeRetry,
                currentLesson.Steps.SelectMany(step => Enumerable.Repeat(Cue(step), step.RequiredCount)).ToArray());
        }

        string FormatLessonText(string text, TutorialVoiceInput input) {
            string Label(string clip) => clip switch {
                "key_i" => "Iキー", "key_j" => "Jキー", "key_k" => "Kキー", "key_l" => "Lキー",
                "key_u" => "Uキー", "button_a" => "Aボタン", "button_b" => "Bボタン",
                "button_x" => "Xボタン", "button_y" => "Yボタン", "button_start" => "スタートボタン",
                _ => throw new ArgumentOutOfRangeException(nameof(clip), clip, "Unknown tutorial input."),
            };
            var keyboard = gamePadRegistry.Get(TUTORIAL_PLAYER_ID).DeviceName == "Keybord";
            var facingRight = !TryResolveTutorialStriker(out var striker) || striker.LookDirection.CurrentValue.x >= 0f;
            return text.Replace("{HoldUp}", keyboard ? "Wキーを押したまま" : "スティックを上に倒したまま")
                .Replace("{HoldDown}", keyboard ? "Sキーを押したまま" : "スティックを下に倒したまま")
                .Replace("{HoldForward}", keyboard ? (facingRight ? "Dキーを押したまま" : "Aキーを押したまま") : "スティックを前に倒したまま")
                .Replace("{Attack}", Label(input.East)).Replace("{Charge}", Label(input.South))
                .Replace("{Dash}", Label(input.West)).Replace("{Guard}", Label(input.North))
                .Replace("{Jump}", Label(input.West))
                .Replace("{Special}", Label(input.Start))
                .Replace("{Up}", keyboard ? "Wキー" : "上方向")
                .Replace("{Down}", keyboard ? "Sキー" : "下方向")
                .Replace("{Forward}", keyboard ? (facingRight ? "Dキー" : "Aキー") : "前方向")
                .Replace("{Horizontal}", keyboard ? "A / D キー" : "左右方向");
        }

        async Task RunTutorialSequenceAsync() {
            try {
                Debug.Log($"{LOG_PREFIX} RunTutorialSequenceAsync started");

                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await AwaitWhileActiveAsync(RunDescriptionSequenceAsync(TutorialUiKey.Overview));
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

                await AwaitWhileActiveAsync(RunCharacterLessonsAsync());

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

                await AwaitWhileActiveAsync(RunDescriptionSequenceAsync(TutorialUiKey.Final));
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

        async Task RunCharacterLessonsAsync() {
            if (!TryResolveTutorialStriker(out var striker) || striker is not IStrikerTutorialSource source ||
                source.TutorialLessons.Count == 0) {
                throw new InvalidOperationException("Tutorial player has no character lessons configured.");
            }

            RequestTutorialPause();
            source.ResetTutorialPractice(TutorialReset.All);
            for (var i = 0; i < source.TutorialLessons.Count; i++) {
                if (disposed || !tutorialSetting.IsTutorialBattleRequested) return;
                var lesson = source.TutorialLessons[i];
                currentLesson = lesson;
                showingAfterPractice = false;
                currentDescriptionPage = 0;
                currentPracticeStep = lesson.Steps[0];
                currentPracticeCount = 0;
                currentPracticeRetry = false;
                RefreshTutorialInput();
                RequestTutorialPause();
                source.ResetTutorialPractice(lesson.ResetBeforePractice);
                if (lesson.IsSpecial) striker.AddSpecialPoint(float.MaxValue);
                await AwaitWhileActiveAsync(RunActionStepAsync(
                    TutorialUiKey.CharacterDescription, TutorialUiKey.CharacterPractice,
                    () => WaitForCharacterActionAsync(striker, source, lesson)));
                showingAfterPractice = true;
                for (var page = 0; page < lesson.AfterPracticePages.Length; page++) {
                    currentDescriptionPage = page;
                    RefreshTutorialInput();
                    tutorialView.SetCharacterLessonNarration(new[] { lesson.AfterPracticePages[page].Voice });
                    await ShowDescriptionAndWaitForEastAsync(TutorialUiKey.CharacterDescription);
                }
            }
            currentLesson = null;
            currentPracticeStep = null;
        }

        async Task WaitForCharacterActionAsync(IStrikerHub striker, IStrikerTutorialSource source, TutorialActionLesson lesson) {
            var progress = new TutorialLessonProgress(lesson, source.TutorialCommandId);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var practiceSubscriptions = new CompositeDisposable();
            var retryPending = false;
            void ShowProgress(bool retry = false) {
                if (progress.IsComplete) return;
                currentPracticeStep = progress.Step;
                currentPracticeCount = progress.Count;
                currentPracticeRetry = retry;
                RefreshPracticeInput();
            }
            void Retry() {
                retryPending = true;
                ShowProgress(true);
            }
            ShowProgress();
            source.OnTutorialCommandExecuted.Subscribe(result => {
                if (retryPending || completion.Task.IsCompleted) return;
                // An ignored button press does not invalidate an already completed setup.
                if (progress.RequiresRetryAfterCommand(result.Command, result.EnteredState)) Retry();
            }).AddTo(practiceSubscriptions);
            foreach (var state in lesson.Steps.SelectMany(step => step.TargetStates).Distinct()) {
                state.OnTutorialExecution.Subscribe(execution => {
                    if (retryPending || completion.Task.IsCompleted) return;
                    switch (progress.Observe(execution)) {
                        case TutorialProgressResult.Retry: Retry(); break;
                        case TutorialProgressResult.Advanced: ShowProgress(); break;
                        case TutorialProgressResult.Complete: completion.TrySetResult(true); break;
                    }
                }).AddTo(practiceSubscriptions);
            }
            Observable.EveryUpdate().Subscribe(_ => {
                var attentionActive = battlePresenter.IsAttentionPresentationActive;
                tutorialView.SetAttentionActive(attentionActive);
                if (disposed || !tutorialSetting.IsTutorialBattleRequested) {
                    completion.TrySetResult(false);
                    return;
                }
                if (completion.Task.IsCompleted) return;
                var category = source.ObservedState.Category;
                if (lesson.IsSpecial && category != StrikerStateCategory.Special && !attentionActive &&
                    striker.SpecialPoint.CurrentValue < striker.MaxSpecialPoint.CurrentValue) {
                    // A rejected input or an approach that timed out can spend SP.
                    striker.AddSpecialPoint(float.MaxValue);
                }
                if (retryPending && category == StrikerStateCategory.Idle && !attentionActive) {
                    source.ResetTutorialPractice(TutorialReset.All);
                    progress.Reset(source.TutorialCommandId);
                    retryPending = false;
                    ShowProgress();
                }
                if (!retryPending && !progress.IsComplete && progress.StepIndex > 0 &&
                    lesson.ResetBeforePractice == TutorialReset.None && !lesson.Steps[0].CompleteOnExit &&
                    category == StrikerStateCategory.Idle && source.IsTutorialGrounded) {
                    // Landing or letting a temporary stance expire ends the setup.
                    // Prompt for that setup again instead of waiting for an impossible follow-up.
                    progress.Reset(source.TutorialCommandId);
                    ShowProgress();
                }
            }).AddTo(practiceSubscriptions);
            await AwaitWhileActiveAsync(completion.Task);
            if (lesson.IsSpecial && !disposed && tutorialSetting.IsTutorialBattleRequested) {
                var presentationCompleted = new TaskCompletionSource<bool>();
                using var specialSubscription = Observable.EveryUpdate().Subscribe(_ => {
                    if (!tutorialSetting.IsTutorialBattleRequested ||
                        (source.ObservedState.Category != StrikerStateCategory.Special && !battlePresenter.IsAttentionPresentationActive))
                        presentationCompleted.TrySetResult(true);
                });
                await AwaitWhileActiveAsync(presentationCompleted.Task);
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
            var pageCount = descriptionKey == TutorialUiKey.CharacterDescription ? currentLesson.Pages.Length : tutorialView.GetDescriptionPageCount(descriptionKey);
            for (var page = 0; page < pageCount; page++) {
                if (descriptionKey == TutorialUiKey.CharacterDescription) {
                    currentDescriptionPage = page;
                    RefreshTutorialInput();
                    tutorialView.SetCharacterLessonNarration(new[] { currentLesson.Pages[page].Voice });
                }
                else tutorialView.SetDescriptionPage(descriptionKey, page);
                await ShowDescriptionAndWaitForEastAsync(descriptionKey);
                if (!tutorialSetting.IsTutorialBattleRequested) break;
            }
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
                for (var page = 0; page < tutorialView.GetDescriptionPageCount(descriptionKeys[i]); page++) {
                    tutorialView.SetDescriptionPage(descriptionKeys[i], page);
                    await ShowDescriptionAndWaitForEastAsync(descriptionKeys[i]);

                    if (!tutorialSetting.IsTutorialBattleRequested) break;
                }

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

        async Task ShowDescriptionAndWaitForEastAsync(TutorialUiKey key) {
            var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = gamePadRegistry.Get(TUTORIAL_PLAYER_ID).OnButtonDown
                .Where(button => button == GamePadButton.East)
                .Take(1)
                .Subscribe(_ => confirmation.TrySetResult(true));
            // The confirmation hint is visible while the page fades in.
            await AwaitWhileActiveAsync(tutorialView.ShowAsync(key));
            if (tutorialSetting.IsTutorialBattleRequested) await AwaitWhileActiveAsync(confirmation.Task);
        }

        async Task WaitForRhythmAsync() {
            var beatPlayer = beatJudge.GetBeatPlayer(TUTORIAL_PLAYER_ID);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var successCount = 0;
            var previousBeatIndex = -1;
            var previousPlaybackTime = -1f;
            using var practiceSubscriptions = new CompositeDisposable();

            void ResetProgress() {
                successCount = 0;
                previousBeatIndex = -1;
                tutorialView.SetRhythmProgress(0, RHYTHM_REQUIRED_SUCCESSES);
            }

            void ObservePlayback(float time) {
                if (time < previousPlaybackTime) ResetProgress();
                previousPlaybackTime = time;
            }

            beatPlayer.OnBeatCommandRequested
                .Where(result => !result.IsSuccess)
                .Subscribe(result => {
                    if (completion.Task.IsCompleted) return;
                    previousPlaybackTime = result.Time;
                    ResetProgress();
                }).AddTo(practiceSubscriptions);
            beatPlayer.OnBeatPassed.Subscribe(result => {
                if (completion.Task.IsCompleted) return;
                ObservePlayback(result.Time);
                // A late deadline for an earlier beat can arrive after an early hit.
                if (result.BeatIndex > previousBeatIndex && successCount > 0) ResetProgress();
            }).AddTo(practiceSubscriptions);
            beatPlayer.OnBeatCommandExecuted.Subscribe(result => {
                if (completion.Task.IsCompleted) return;
                ObservePlayback(result.Time);
                if (!result.IsSuccess || result.Button is not (GamePadButton.North or GamePadButton.East or GamePadButton.South or GamePadButton.West)) {
                    ResetProgress();
                    return;
                }
                if (result.BeatIndex == previousBeatIndex) return;
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

        async Task WaitForWalkAsync() {
            if (!TryResolveTutorialStriker(out var striker)) {
                throw new InvalidOperationException("Tutorial player is not deployed.");
            }

            var threshold = tutorialView.WalkVelocityThreshold;
            var requiredTravelDistance = tutorialView.WalkPracticeRequiredTravelDistance;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var previousPosition = striker.Position.CurrentValue;
            var travelDistance = 0f;

            using var subscription = Observable.EveryUpdate()
                .Subscribe(_ => {
                    if (!tutorialSetting.IsTutorialBattleRequested) {
                        completion.TrySetResult(false);
                        return;
                    }
                    if (completion.Task.IsCompleted) return;

                    var currentPosition = striker.Position.CurrentValue;
                    var stepDistance = new Vector2(
                        currentPosition.x - previousPosition.x,
                        currentPosition.z - previousPosition.z).magnitude;
                    previousPosition = currentPosition;

                    // Judge speed over the same update interval as the measured displacement.
                    var elapsed = Time.deltaTime;
                    if (elapsed <= 0f || stepDistance < threshold * elapsed) {
                        return;
                    }

                    travelDistance += stepDistance;
                    if (travelDistance < requiredTravelDistance) {
                        return;
                    }

                    completion.TrySetResult(true);
                });

            await AwaitWhileActiveAsync(completion.Task);
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
