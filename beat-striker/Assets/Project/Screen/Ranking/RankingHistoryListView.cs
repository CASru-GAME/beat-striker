using System.Collections.Generic;
using PolyAndCode.UI;
using UnityEngine;

namespace Alice {
    /// <summary>
    /// 対戦履歴リストの表示専用。データはモック（本番はプレゼンター経由の差し替えを想定）。
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class RankingHistoryListView : MonoBehaviour, IRecyclableScrollRectDataSource {
        const int MockEntryCount = 48;

        [SerializeField] RecyclableScrollRect scrollRect;

        readonly List<RankingBattleHistoryEntry> entries = new();

        void Awake() {
            FillMockEntries();
            scrollRect.DataSource = this;
        }

        void FillMockEntries() {
            entries.Clear();
            for (var i = 0; i < MockEntryCount; i++) {
                var index = i + 1;
                var day = index % 28 + 1;
                var hour = 10 + index % 12;
                var minute = index * 7 % 60;
                var playedAt = $"2026/05/{day:D2} {hour:D2}:{minute:D2}";
                entries.Add(new RankingBattleHistoryEntry(
                    $"ストライカー{index}",
                    $"ライバル{index}",
                    playedAt));
            }
        }

        public int GetItemCount() {
            return entries.Count;
        }

        public void SetCell(ICell cell, int index) {
            var row = (RankingHistoryCellView)cell;
            row.ConfigureCell(entries[index]);
        }
    }
}
