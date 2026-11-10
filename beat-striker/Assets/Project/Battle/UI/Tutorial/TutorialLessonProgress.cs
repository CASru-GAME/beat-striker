using System;
using System.Collections.Generic;

namespace Alice {
    public enum TutorialProgressResult { Ignored, Advanced, Retry, Complete }

    public sealed class TutorialLessonProgress {
        readonly TutorialActionLesson lesson;
        readonly HashSet<(StrikerState State, int Execution)> counted = new();
        int minimumCommandId;
        bool shieldLaunched;
        public int StepIndex { get; private set; }
        public int Count { get; private set; }
        public bool IsComplete => StepIndex == lesson.Steps.Length;
        public TutorialActionStep Step => lesson.Steps[StepIndex];

        public TutorialLessonProgress(TutorialActionLesson lesson, int minimumCommandId) {
            this.lesson = lesson;
            Reset(minimumCommandId);
        }

        public void Reset(int commandId) {
            minimumCommandId = commandId;
            StepIndex = Count = 0;
            shieldLaunched = false;
            counted.Clear();
        }

        public TutorialProgressResult Observe(TutorialStateExecution execution) {
            if (IsComplete || execution.Command.Id <= minimumCommandId) return TutorialProgressResult.Ignored;
            // A jump can land or a stance can expire before the follow-up is entered.
            // Let the player begin that resource-free sequence again on the same input.
            if (!execution.Exited && StepIndex > 0 && lesson.ResetBeforePractice == TutorialReset.None &&
                lesson.Steps[0].Matches(execution.Command) &&
                Array.Exists(lesson.Steps[0].TargetStates, state => ReferenceEquals(state, execution.State)))
                Reset(execution.Command.Id - 1);
            var step = Step;
            if (!step.Matches(execution.Command) ||
                !Array.Exists(step.TargetStates, state => ReferenceEquals(state, execution.State)))
                return TutorialProgressResult.Ignored;

            if (execution.Exited != step.CompleteOnExit || !counted.Add((execution.State, execution.ExecutionId)))
                return TutorialProgressResult.Ignored;

            if (step.RequiresCommittedAction && !execution.Committed) {
                // A failed follow-up can already have spent the accumulated charge.
                return execution.Exited && StepIndex > 0 && (lesson.ResetBeforePractice & TutorialReset.Charge) != 0
                    ? TutorialProgressResult.Retry : TutorialProgressResult.Ignored;
            }
            shieldLaunched |= execution.ShieldLaunched;
            if (Count < step.RequiredCount) Count++;
            if (Count == step.RequiredCount && (!step.RequiresShieldLaunch || shieldLaunched)) {
                StepIndex++;
                Count = 0;
                shieldLaunched = false;
            }
            return IsComplete ? TutorialProgressResult.Complete : TutorialProgressResult.Advanced;
        }

        // Exiting the previous action can advance the step before the new attack is checked.
        public bool RequiresRetryAfterCommand(TutorialCommandContext command, IStrikerState enteredState) {
            if (IsComplete || command.Id <= minimumCommandId) return false;
            return lesson.Steps.Length > 1 && command.Button == GamePadButton.East &&
                enteredState?.Category == StrikerStateCategory.Attack &&
                (!Step.Matches(command) || !Array.Exists(Step.TargetStates, state => ReferenceEquals(state, enteredState)));
        }
    }
}
