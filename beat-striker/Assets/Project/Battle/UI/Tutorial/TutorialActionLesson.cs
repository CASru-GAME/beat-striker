using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Alice {
    [Serializable]
    public record TutorialActionLesson {
        public string Title;
        public TutorialLessonPage[] Pages;
        [TextArea] public string PracticeInstruction;
        public TutorialLessonPage[] AfterPracticePages;
        public TutorialActionStep[] Steps;
        public TutorialReset ResetBeforePractice;
        public bool IsSpecial;
    }

    [Serializable]
    public record TutorialLessonPage {
        [TextArea] public string Text;
        public string Voice;
    }

    [Flags]
    public enum TutorialReset { None = 0, Charge = 1, Guard = 2, All = Charge | Guard }
    public enum TutorialDirection { Any, Neutral, Up, ForwardUp, Forward, Down, NoHorizontal, NoVertical }
    public enum TutorialGround { Any, Grounded, Airborne }

    [Serializable]
    public record TutorialActionStep {
        public string Label;
        public string Instruction;
        public GamePadButton Command;
        public TutorialDirection Direction;
        public TutorialGround Ground;
        public StrikerState[] TargetStates;
        [Min(1)] public int RequiredCount = 1;
        public bool CompleteOnExit = true;
        public bool RequiresCommittedAction;
        public bool RequiresShieldLaunch;

        public bool Matches(TutorialCommandContext command) {
            if (command.Button != Command) return false;
            if (Ground == TutorialGround.Grounded && !command.IsGrounded) return false;
            if (Ground == TutorialGround.Airborne && command.IsGrounded) return false;
            var direction = command.LocalDirection;
            return Direction switch {
                TutorialDirection.Any => true,
                TutorialDirection.Neutral => direction.sqrMagnitude < 0.01f,
                TutorialDirection.Up => direction.y > 0.5f && Mathf.Abs(direction.x) < 0.5f,
                TutorialDirection.ForwardUp => direction.x > 0.5f && direction.y > 0.5f,
                TutorialDirection.Forward => direction.x > 0.5f && Mathf.Abs(direction.y) < 0.5f,
                TutorialDirection.Down => direction.y < -0.5f,
                TutorialDirection.NoHorizontal => direction.x == 0f,
                TutorialDirection.NoVertical => Mathf.Abs(direction.y) <= 0.5f,
                _ => false,
            };
        }
    }

    public record TutorialCommandContext(int Id, GamePadButton Button, Vector2 LocalDirection, bool IsGrounded);
    public record TutorialStateExecution(StrikerState State, int ExecutionId, TutorialCommandContext Command,
        bool Exited, bool Committed, bool ShieldLaunched);

    public interface ITutorialGroundSource { bool IsGrounded { get; } }
    public interface ITutorialResourceReset {
        TutorialReset Resource { get; }
        void ResetTutorialResource();
    }
    public interface ITutorialGuardProgress { bool HasLaunchedShield { get; } }

    public interface IStrikerTutorialSource : IStrikerCombatObservationSource {
        IReadOnlyList<TutorialActionLesson> TutorialLessons { get; }
        Observable<TutorialCommandContext> OnTutorialCommandExecuted { get; }
        TutorialCommandContext CaptureTutorialCommand();
        void ResetTutorialPractice(TutorialReset reset);
    }

    // Confirms that charging or releasing an attack actually took effect.
    public interface ITutorialActionProgress {
        bool IsTutorialActionCommitted { get; }
    }
}
