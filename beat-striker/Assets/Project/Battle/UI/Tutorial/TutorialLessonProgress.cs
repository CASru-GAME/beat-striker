using System;
using System.Collections.Generic;

namespace Alice {
    public enum TutorialProgressResult { Ignored, Advanced, Retry, Complete }

    public sealed class TutorialLessonProgress {
        readonly TutorialActionLesson lesson;
        readonly HashSet<(StrikerState State, int Execution)> counted = new();
        int minimumCommandId;
        int lastMatchedCommandId;
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
            lastMatchedCommandId = commandId;
            StepIndex = Count = 0;
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

            lastMatchedCommandId = Math.Max(lastMatchedCommandId, execution.Command.Id);
            if (execution.Exited != step.CompleteOnExit || (step.RequiresCommittedAction && !execution.Committed) ||
                !counted.Add((execution.State, execution.ExecutionId))) return TutorialProgressResult.Ignored;

            if (Count + 1 == step.RequiredCount && step.RequiresShieldLaunch && !execution.ShieldLaunched)
                return TutorialProgressResult.Retry;
            Count++;
            if (Count == step.RequiredCount) {
                StepIndex++;
                Count = 0;
            }
            return IsComplete ? TutorialProgressResult.Complete : TutorialProgressResult.Advanced;
        }

        // Called after the command has finished transitioning states: exiting the
        // previous action can advance the expected step before this input is checked.
        public bool IsUnexpectedCommand(TutorialCommandContext command) =>
            !IsComplete && command.Id > lastMatchedCommandId && !Step.Matches(command);

        public bool RequiresRetryAfterCommand(TutorialCommandContext command, IStrikerState performedState) {
            if (IsComplete) return false;
            var wrongAttack = lesson.Steps.Length > 1 && command.Button == GamePadButton.East &&
                performedState.Category == StrikerStateCategory.Attack &&
                (!Step.Matches(command) || !Array.Exists(Step.TargetStates, state => ReferenceEquals(state, performedState)));
            var wrongSpecial = lesson.IsSpecial && command.Button == GamePadButton.Start &&
                performedState.Category == StrikerStateCategory.Special && IsUnexpectedCommand(command);
            return wrongAttack || wrongSpecial;
        }
    }
}
