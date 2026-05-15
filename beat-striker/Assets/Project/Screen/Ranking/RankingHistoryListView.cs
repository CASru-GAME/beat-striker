using System.Collections.Generic;
using PolyAndCode.UI;
using R3;
using UnityEngine;

namespace Alice {
    [DefaultExecutionOrder(-40)]
    public class RankingHistoryListView : MonoBehaviour, IRecyclableScrollRectDataSource {
        [SerializeField] RecyclableScrollRect scrollRect;

        readonly List<RankingBattleHistoryEntry> entries = new();
        readonly Subject<string> replayRequested = new();

        public Observable<string> ReplayRequested => replayRequested;

        void Awake() {
            scrollRect.DataSource = this;
        }

        public void SetEntries(IEnumerable<RankingBattleHistoryEntry> nextEntries) {
            entries.Clear();
            if (nextEntries != null) {
                entries.AddRange(nextEntries);
            }

            scrollRect.ReloadData();
        }

        public int GetItemCount() {
            return entries.Count;
        }

        public void SetCell(ICell cell, int index) {
            var row = (RankingHistoryCellView)cell;
            row.ConfigureCell(entries[index], replayRequested.OnNext);
        }

        void OnDestroy() {
            replayRequested.Dispose();
        }
    }
}
