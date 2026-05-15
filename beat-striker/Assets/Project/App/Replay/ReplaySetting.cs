namespace Alice {
    public interface IReplaySetting {
        bool HasReplay { get; }
        bool TryGetReplay(out ReplayPayload replayPayload);
        void SetReplay(ReplayPayload replayPayload);
        void ClearReplay();
    }

    public class ReplaySetting : IReplaySetting {
        ReplayPayload replayPayload;

        public bool HasReplay => replayPayload != null;

        public bool TryGetReplay(out ReplayPayload replayPayload) {
            replayPayload = this.replayPayload;
            return replayPayload != null;
        }

        public void SetReplay(ReplayPayload replayPayload) {
            this.replayPayload = replayPayload;
        }

        public void ClearReplay() {
            replayPayload = null;
        }
    }
}
