namespace Alice {
    public readonly struct OnlineMatchRequest {
        public readonly Striker LocalStriker;
        public readonly Stage CandidateStage;
        public readonly string CandidateMusicId;

        public OnlineMatchRequest(Striker localStriker, Stage candidateStage, string candidateMusicId) {
            LocalStriker = localStriker;
            CandidateStage = candidateStage;
            CandidateMusicId = candidateMusicId;
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
