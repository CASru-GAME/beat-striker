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
        readonly BattleTutorialView tutorialView;
        readonly CompositeDisposable subscriptions = new();
        readonly Subject<Unit> tutorialPauseRequestedSubject = new();
        readonly Subject<Unit> tutorialResumeRequestedSubject = new();
        readonly Subject<Unit> tutorialEndBattleToTitleRequestedSubject = new();
        bool started;

        public Observable<Unit> OnTutorialPauseRequested => tutorialPauseRequestedSubject;
        public Observable<Unit> OnTutorialResumeRequested => tutorialResumeRequestedSubject;
        public Observable<Unit> OnTutorialEndBattleToTitleRequested => tutorialEndBattleToTitleRequestedSubject;

        [Inject]
        public BattleTutorialPresenter(
            IBeatjudge beatJudge,
            IStrikerRegistry strikerRegistry,
            IGamePadRegistry gamePadRegistry,
            ITutorialSetting tutorialSetting,
            BattleTutorialView tutorialView) {
            this.beatJudge = beatJudge;
            this.strikerRegistry = strikerRegistry;
            this.gamePadRegistry = gamePadRegistry;
            this.tutorialSetting = tutorialSetting;
            this.tutorialView = tutorialView;

            if (!tutorialSetting.IsTutorialBattleRequested) {
                tutorialView.HideAll();
                return;
            }

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

        async Task RunTutorialSequenceAsync() {
            try {
                Debug.Log($"{LOG_PREFIX} RunTutorialSequenceAsync started");

                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                RequestTutorialPause();
                await tutorialView.ShowAsync(TutorialUiKey.Overview);
                await WaitForEastAsync();
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                tutorialView.HideAll();
                await RunDescriptionSequenceAsync(
                    TutorialUiKey.HpDescription,
                    TutorialUiKey.SpDescription);
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                RequestTutorialResume();

                await RunActionStepAsync(
                    TutorialUiKey.WalkDescription,
                    TutorialUiKey.WalkPractice,
                    WaitForWalkAsync);

                await RunActionStepAsync(
                    TutorialUiKey.DashDescription,
                    TutorialUiKey.DashPractice,
                    () => WaitForStateCategoryAsync(StrikerStateCategory.Dash));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await RunActionStepAsync(
                    TutorialUiKey.AttackDescription,
                    TutorialUiKey.AttackPractice,
                    () => WaitForStateCategoryAsync(StrikerStateCategory.Attack));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await RunActionStepAsync(
                    TutorialUiKey.ChargeDescription,
                    TutorialUiKey.ChargePractice,
                    () => WaitForStateCategoryAsync(StrikerStateCategory.Charge));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await RunActionStepAsync(
                    TutorialUiKey.ChargeAttackDescription,
                    TutorialUiKey.ChargeAttackPractice,
                    WaitForChargeAttackAsync);
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                await RunActionStepAsync(
                    TutorialUiKey.GuardDescription,
                    TutorialUiKey.GuardPractice,
                    () => WaitForStateCategoryAsync(StrikerStateCategory.Guard));
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                if (!TryFillSpecialPointMax()) {
                    tutorialView.HideAll();
                    return;
                }

                await RunActionStepAsync(
                    TutorialUiKey.SpecialDescription,
                    TutorialUiKey.SpecialPractice,
                    () => WaitForStateCategoryAsync(StrikerStateCategory.Special));

                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                RequestTutorialPause();
                await tutorialView.ShowAsync(TutorialUiKey.Final);
                await tutorialView.HideAfterFinalDelayAsync();
                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }

                tutorialView.HideAll();
                tutorialSetting.ClearTutorialBattleRequest();
                tutorialEndBattleToTitleRequestedSubject.OnNext(Unit.Default);

                Debug.Log($"{LOG_PREFIX} RunTutorialSequenceAsync completed");
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
            if (!tutorialSetting.IsTutorialBattleRequested) {
                return;
            }

            RequestTutorialPause();
            await tutorialView.ShowAsync(descriptionKey);
            await WaitForEastAsync();
            if (!tutorialSetting.IsTutorialBattleRequested) {
                tutorialView.HideAll();
                return;
            }

            await tutorialView.ShowAsync(practiceKey);
            RequestTutorialResume();
            await waitActionAsync();
            if (!tutorialSetting.IsTutorialBattleRequested) {
                tutorialView.HideAll();
                return;
            }

            RequestTutorialPause();
            await tutorialView.HideAfterClearDelayAsync(practiceKey);
        }

        async Task RunDescriptionSequenceAsync(params TutorialUiKey[] descriptionKeys) {
            if (!tutorialSetting.IsTutorialBattleRequested) {
                return;
            }

            RequestTutorialPause();

            for (var i = 0; i < descriptionKeys.Length; i++) {
                await tutorialView.ShowAsync(descriptionKeys[i]);
                await WaitForEastAsync();

                if (!tutorialSetting.IsTutorialBattleRequested) {
                    tutorialView.HideAll();
                    return;
                }
            }

            tutorialView.HideAll();
            RequestTutorialResume();
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

            return tcs.Task;
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

            return tcs.Task;
        }

        async Task WaitForChargeAttackAsync() {
            await WaitForStateCategoryAsync(StrikerStateCategory.Charge);
            await WaitForStateCategoryAsync(StrikerStateCategory.Attack);
        }

        Task WaitForStateCategoryAsync(StrikerStateCategory targetCategory) {
            if (!TryResolveTutorialStriker(out var striker)) {
                return Task.CompletedTask;
            }

            var hasLeftTargetState = striker.CurrentStateCategory.CurrentValue != targetCategory;
            var tcs = new TaskCompletionSource<bool>();

            IDisposable subscription = null;
            subscription = striker.CurrentStateCategory
                .Subscribe(category => {
                    if (!tutorialSetting.IsTutorialBattleRequested) {
                        subscription.Dispose();
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

                    subscription.Dispose();
                    tcs.TrySetResult(true);
                });

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
            subscriptions.Dispose();
            tutorialPauseRequestedSubject.Dispose();
            tutorialResumeRequestedSubject.Dispose();
            tutorialEndBattleToTitleRequestedSubject.Dispose();
            tutorialView.HideAll();
        }
    }
}
