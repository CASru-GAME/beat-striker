using System;
using System.Collections.Generic;
using System.Linq;

namespace Alice {
    public record RankingNavigationState {
        public string PlayerName { get; set; } = "";
        public string HistoryCursor { get; set; } = "";
        public float ListPosition { get; set; } = 1;
        public float HistoryRowPosition { get; set; }
        public RankingBattleHistoryEntry[] Entries { get; set; } = Array.Empty<RankingBattleHistoryEntry>();
        public PlayerProfile[] RankingEntries { get; private set; } = Array.Empty<PlayerProfile>();
        public string[] RankingCursors { get; private set; } = Array.Empty<string>();
        public bool HasRankingSnapshot => RankingCursors.Length > 0;

        public void CaptureRanking(IEnumerable<PlayerProfile> entries, string[] cursors) {
            RankingEntries = entries.Select(profile => profile with { }).ToArray();
            RankingCursors = (string[])cursors.Clone();
        }

        public void Clear() {
            PlayerName = "";
            Entries = Array.Empty<RankingBattleHistoryEntry>();
            RankingEntries = Array.Empty<PlayerProfile>();
            RankingCursors = Array.Empty<string>();
            ListPosition = 1;
            HistoryRowPosition = 0;
            HistoryCursor = "";
        }
    }
}
