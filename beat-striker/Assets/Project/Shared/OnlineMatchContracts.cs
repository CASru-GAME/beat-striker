namespace Alice {
    public readonly struct OnlineMatchRequest {
        public readonly Striker LocalStriker;
        public readonly Stage CandidateStage;
        public readonly string CandidateMusicId;
        public readonly string ReservationId;
        public readonly string DuelSessionId;

        public OnlineMatchRequest(Striker localStriker, Stage candidateStage, string candidateMusicId) {
            LocalStriker = localStriker;
            CandidateStage = candidateStage;
            CandidateMusicId = candidateMusicId;
            ReservationId = "";
            DuelSessionId = "";
        }

        public OnlineMatchRequest(Striker localStriker, Stage candidateStage, string candidateMusicId, string reservationId, string duelSessionId) {
            LocalStriker = localStriker;
            CandidateStage = candidateStage;
            CandidateMusicId = candidateMusicId;
            ReservationId = reservationId ?? "";
            DuelSessionId = duelSessionId ?? "";
        }
    }

    public readonly struct OnlineMatchResult {
        public readonly Striker LocalStriker;
        public readonly Striker OpponentStriker;
        public readonly Stage Stage;
        public readonly string MusicId;
        public readonly bool LocalIsPlayer1;

        public OnlineMatchResult(
            Striker localStriker,
            Striker opponentStriker,
            Stage stage,
            string musicId,
            bool localIsPlayer1) {
            LocalStriker = localStriker;
            OpponentStriker = opponentStriker;
            Stage = stage;
            MusicId = musicId;
            LocalIsPlayer1 = localIsPlayer1;
        }
    }
}
