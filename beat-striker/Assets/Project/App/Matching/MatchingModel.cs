using R3;

namespace Alice {
    public enum MatchingPhase {
        Idle,
        InvitingOrGuidance,
        StageSelecting,
        CharacterSelecting,
        Waiting,
        InBattle,
        Error,
    }

    public record MatchingState(
        MatchingPhase LocalPhase,
        string LocalSessionId,
        string ReservationId,
        string OpponentSessionId,
        MatchingPhase OpponentPhase,
        OnlineDuelPlayerStatus OpponentStatus,
        float MatchDeadlineRealtime,
        int LastSceneSyncId,
        string IncomingInviteId,
        string IncomingFromSessionId,
        string InviteCandidateSessionId,
        bool IsCandidateGuidance,
        string Message) {
        public static MatchingState Idle(string message = "") {
            return new MatchingState(
                MatchingPhase.Idle,
                "",
                "",
                "",
                MatchingPhase.Idle,
                OnlineDuelPlayerStatus.StageSelecting,
                0f,
                0,
                "",
                "",
                "",
                false,
                message ?? "");
        }

        public MatchingPhase Phase => LocalPhase;
        public bool IsEstablished => HasReservation || Phase == MatchingPhase.InBattle;
        public bool IsActive => Phase != MatchingPhase.Idle;
        public bool IsPreBattleMatched => HasReservation && Phase != MatchingPhase.InBattle;
        public bool HasReservation => !string.IsNullOrWhiteSpace(ReservationId);
        public bool HasIncomingInvite => !string.IsNullOrWhiteSpace(IncomingInviteId);
        public bool HasInviteCandidate => !string.IsNullOrWhiteSpace(InviteCandidateSessionId);
    }

    public interface IMatchingModel {
        ReadOnlyReactiveProperty<MatchingState> State { get; }
        ReadOnlyReactiveProperty<bool> IsEstablished { get; }
    }

    public interface IMutableMatchingModel : IMatchingModel {
        void SetState(MatchingState next);
        void Clear(string message);
    }

    public class MatchingModel : IMutableMatchingModel {
        readonly ReactiveProperty<MatchingState> state;
        readonly ReactiveProperty<bool> isEstablished;

        public ReadOnlyReactiveProperty<MatchingState> State => state;
        public ReadOnlyReactiveProperty<bool> IsEstablished => isEstablished;

        public MatchingModel() {
            state = new ReactiveProperty<MatchingState>(MatchingState.Idle());
            isEstablished = new ReactiveProperty<bool>(state.CurrentValue.IsEstablished);
        }

        public void SetState(MatchingState next) {
            state.OnNext(next);
            isEstablished.OnNext(next.IsEstablished);
        }

        public void Clear(string message) {
            SetState(MatchingState.Idle(message));
        }
    }
}
