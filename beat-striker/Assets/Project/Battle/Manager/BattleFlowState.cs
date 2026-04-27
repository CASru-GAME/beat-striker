using UnityEngine;

namespace Alice {
    public enum BattleFlowState {
        NotStarted,
        Opening,
        RoundStarting,
        Playing,
        Suspended,
        AttentionSuspended,
        TutorialSuspended,
        ResolvingRound,
        EndingBattle,
        EndingToTitle,
        Finished,
    }

    public sealed class BattleFlowStateMachine {
        const string LOG_PREFIX = "[BattleFlowState]";

        public BattleFlowState Current { get; private set; } = BattleFlowState.NotStarted;

        public bool IsPlaying => Current == BattleFlowState.Playing;
        public bool IsRoundStarting => Current == BattleFlowState.RoundStarting;
        public bool IsSuspended => Current == BattleFlowState.Suspended;
        public bool IsAttentionSuspended => Current == BattleFlowState.AttentionSuspended;
        public bool IsTutorialSuspended => Current == BattleFlowState.TutorialSuspended;
        public bool IsRoundResolving => Current == BattleFlowState.ResolvingRound;
        public bool IsBattleEndingOrFinished => Current == BattleFlowState.EndingBattle
            || Current == BattleFlowState.EndingToTitle
            || Current == BattleFlowState.Finished;

        public bool CanStartBattle => Current == BattleFlowState.NotStarted;
        public bool CanPauseForSuspend => Current == BattleFlowState.Playing;
        public bool CanSuspendBattle => Current == BattleFlowState.Suspended;
        public bool CanResumeFromSuspend => Current == BattleFlowState.Suspended;
        public bool CanPauseForAttention => Current == BattleFlowState.Playing;
        public bool CanResumeFromAttention => Current == BattleFlowState.AttentionSuspended;
        public bool CanPauseForTutorial => Current == BattleFlowState.Playing;
        public bool CanResumeFromTutorial => Current == BattleFlowState.TutorialSuspended;
        public bool CanCompleteBattleByMusicEnd => Current == BattleFlowState.Playing;
        public bool CanBeginEndingToTitle => !IsBattleEndingOrFinished;

        public bool TryStartBattle(string trigger) {
            return TryTransition(BattleFlowState.Opening, trigger);
        }

        public bool TryBeginRoundStarting(string trigger) {
            return TryTransition(BattleFlowState.RoundStarting, trigger);
        }

        public bool TryEnterPlaying(string trigger) {
            return TryTransition(BattleFlowState.Playing, trigger);
        }

        public bool TryBeginSuspend(string trigger) {
            return TryTransition(BattleFlowState.Suspended, trigger);
        }

        public bool TryBeginAttentionSuspend(string trigger) {
            return TryTransition(BattleFlowState.AttentionSuspended, trigger);
        }

        public bool TryBeginTutorialSuspend(string trigger) {
            return TryTransition(BattleFlowState.TutorialSuspended, trigger);
        }

        public bool TryBeginResolvingRound(string trigger) {
            return TryTransition(BattleFlowState.ResolvingRound, trigger);
        }

        public bool TryBeginEndingBattle(string trigger) {
            return TryTransition(BattleFlowState.EndingBattle, trigger);
        }

        public bool TryBeginEndingToTitle(string trigger) {
            return TryTransition(BattleFlowState.EndingToTitle, trigger);
        }

        public bool TryMarkFinished(string trigger) {
            return TryTransition(BattleFlowState.Finished, trigger);
        }

        bool TryTransition(BattleFlowState next, string trigger) {
            if (!CanTransition(Current, next)) {
                Debug.LogWarning($"{LOG_PREFIX} Rejected transition. trigger={trigger}, current={Current}, next={next}");
                return false;
            }

            if (Current == next) {
                Debug.Log($"{LOG_PREFIX} Transition skipped because already in state. trigger={trigger}, state={Current}");
                return true;
            }

            Debug.Log($"{LOG_PREFIX} Transition. trigger={trigger}, from={Current}, to={next}");
            Current = next;
            return true;
        }

        static bool CanTransition(BattleFlowState current, BattleFlowState next) {
            if (current == next) {
                return true;
            }

            return current switch {
                BattleFlowState.NotStarted => next == BattleFlowState.Opening,
                BattleFlowState.Opening => next == BattleFlowState.RoundStarting
                    || next == BattleFlowState.EndingToTitle,
                BattleFlowState.RoundStarting => next == BattleFlowState.Playing
                    || next == BattleFlowState.EndingBattle
                    || next == BattleFlowState.EndingToTitle,
                BattleFlowState.Playing => next == BattleFlowState.ResolvingRound
                    || next == BattleFlowState.Suspended
                    || next == BattleFlowState.AttentionSuspended
                    || next == BattleFlowState.TutorialSuspended
                    || next == BattleFlowState.EndingBattle
                    || next == BattleFlowState.EndingToTitle,
                BattleFlowState.Suspended => next == BattleFlowState.Playing
                    || next == BattleFlowState.ResolvingRound
                    || next == BattleFlowState.EndingBattle
                    || next == BattleFlowState.EndingToTitle,
                BattleFlowState.AttentionSuspended => next == BattleFlowState.Playing
                    || next == BattleFlowState.ResolvingRound
                    || next == BattleFlowState.EndingBattle
                    || next == BattleFlowState.EndingToTitle,
                BattleFlowState.TutorialSuspended => next == BattleFlowState.Playing
                    || next == BattleFlowState.ResolvingRound
                    || next == BattleFlowState.EndingToTitle,
                BattleFlowState.ResolvingRound => next == BattleFlowState.RoundStarting
                    || next == BattleFlowState.EndingBattle
                    || next == BattleFlowState.EndingToTitle,
                BattleFlowState.EndingBattle => next == BattleFlowState.Finished,
                BattleFlowState.EndingToTitle => next == BattleFlowState.Finished,
                BattleFlowState.Finished => false,
                _ => false,
            };
        }
    }
}
