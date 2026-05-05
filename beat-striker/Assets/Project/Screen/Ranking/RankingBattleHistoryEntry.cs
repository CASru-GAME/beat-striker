namespace Alice {
    public record RankingBattleHistoryEntry(
        string Id,
        string PlayerAName,
        string PlayerBName,
        string PlayedAtText,
        string ResultText,
        string BattleText,
        bool HasReplay);
}
