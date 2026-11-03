using UnityEditor;
using UnityEngine;

namespace Alice.Editor {
    public static class RankingPreviewTools {
        // Editor-only sample records; never used by the API client or a player build.
        [MenuItem("Beat Striker/Preview/Ranking sample matches")]
        public static void SampleMatches() {
            var list = Object.FindFirstObjectByType<RankingHistoryListView>();
            if (!Application.isPlaying || !list) {
                Debug.LogWarning("Open RankingScene in Play Mode before previewing match records.");
                return;
            }
            list.SetEntries(new[] {
                new RankingBattleHistoryEntry("preview-1", "ビート", "ストライカー", "2026/11/03 20:32", "1P WIN!", "Street / Beat the Night", true),
                new RankingBattleHistoryEntry("preview-2", "リズム", "ビート", "2026/11/03 20:24", "2P WIN!", "Live / Electric Stage", true),
                new RankingBattleHistoryEntry("preview-3", "ストライカー", "リズム", "2026/11/03 20:18", "DRAW", "Street / Beat the Night", false),
                new RankingBattleHistoryEntry("preview-4", "ビート", "リズム", "2026/11/03 20:10", "1P WIN!", "Live / Electric Stage", true),
                new RankingBattleHistoryEntry("preview-5", "ストライカー", "ビート", "2026/11/03 20:02", "2P WIN!", "Street / Beat the Night", true),
                new RankingBattleHistoryEntry("preview-6", "リズム", "ストライカー", "2026/11/03 19:54", "1P WIN!", "Live / Electric Stage", false)
            });
        }

        [MenuItem("Beat Striker/Preview/Ranking empty")]
        public static void Empty() {
            var list = Object.FindFirstObjectByType<RankingHistoryListView>();
            if (Application.isPlaying && list) list.SetEntries(null);
        }
    }
}
