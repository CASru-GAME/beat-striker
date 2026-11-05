using System.Collections.Generic;
using PolyAndCode.UI;
using R3;
using UnityEngine;
using TMPro;

namespace Alice {
    [DefaultExecutionOrder(-40)]
    public class RankingHistoryListView : MonoBehaviour, IRecyclableScrollRectDataSource {
        [SerializeField] RecyclableScrollRect scrollRect;

        TextMeshProUGUI statusLabel;
        string statusHeading = "読み込み中";
        string statusMessage = "対戦履歴を読み込んでいます";
        readonly List<RankingBattleHistoryEntry> entries = new();
        readonly Subject<string> replayRequested = new();

        public Observable<string> ReplayRequested => replayRequested;
        public float RowPosition => scrollRect.VerticalRowPosition;

        void Awake() {
            scrollRect.DataSource = this;
        }

        public void SetStatusLabel(TextMeshProUGUI label) {
            statusLabel = label;
            if (entries.Count == 0) ShowStatus(statusHeading, statusMessage);
            else statusLabel.gameObject.SetActive(false);
        }

        public void ShowStatus(string heading, string message) {
            statusHeading = heading;
            statusMessage = message;
            if (statusLabel != null) {
                statusLabel.gameObject.SetActive(true);
                statusLabel.text = $"{heading}\n<size=18>{message}</size>";
            }
        }

        public void SetEntries(IEnumerable<RankingBattleHistoryEntry> nextEntries, float rowPosition = 0) {
            entries.Clear();
            if (nextEntries != null) {
                entries.AddRange(nextEntries);
            }

            if (entries.Count == 0) ShowStatus("まだ対戦履歴がありません", "バトルの記録がここに表示されます");
            else if (statusLabel != null) statusLabel.gameObject.SetActive(false);
            scrollRect.ReloadData(this, rowPosition);
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
