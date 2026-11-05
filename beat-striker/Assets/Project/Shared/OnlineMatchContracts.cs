namespace Alice {
    public readonly struct OnlineMatchRequest {
        public readonly Striker LocalStriker;
        public readonly Stage CandidateStage;
        public readonly string CandidateMusicId;
        public readonly string ReservationId;
        public readonly string DuelSessionId;
        public readonly PlayerIdentity Player;

        public OnlineMatchRequest(Striker localStriker, Stage candidateStage, string candidateMusicId) {
            LocalStriker = localStriker;
            CandidateStage = candidateStage;
            CandidateMusicId = candidateMusicId;
            ReservationId = "";
            DuelSessionId = "";
            Player = null;
        }

        public OnlineMatchRequest(Striker localStriker, Stage candidateStage, string candidateMusicId, string reservationId, string duelSessionId, PlayerIdentity player = null) {
            LocalStriker = localStriker;
            CandidateStage = candidateStage;
            CandidateMusicId = candidateMusicId;
            ReservationId = reservationId ?? "";
            DuelSessionId = duelSessionId ?? "";
            Player = player;
        }
    }

    public readonly struct OnlineMatchResult {
        public readonly Striker LocalStriker;
        public readonly Striker OpponentStriker;
        public readonly Stage Stage;
        public readonly string MusicId;
        public readonly bool LocalIsPlayer1;
        public readonly PlayerIdentity LocalPlayer;
        public readonly PlayerIdentity OpponentPlayer;
        public readonly string BattleId;

        public OnlineMatchResult(
            Striker localStriker,
            Striker opponentStriker,
            Stage stage,
            string musicId,
            bool localIsPlayer1, PlayerIdentity localPlayer = null, PlayerIdentity opponentPlayer = null, string battleId = "") {
            LocalStriker = localStriker;
            OpponentStriker = opponentStriker;
            Stage = stage;
            MusicId = musicId;
            LocalIsPlayer1 = localIsPlayer1;
            LocalPlayer = localPlayer; OpponentPlayer = opponentPlayer; BattleId = battleId;
        }
    }
}
