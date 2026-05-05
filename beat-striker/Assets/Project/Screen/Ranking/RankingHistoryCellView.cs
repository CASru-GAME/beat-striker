using PolyAndCode.UI;
using R3;
using TMPro;
using UnityEngine;

namespace Alice {
    public class RankingHistoryCellView : MonoBehaviour, ICell {
        [SerializeField] TextMeshProUGUI lineLabel;
        [SerializeField] ActionEmitter replayEmitter;
        [SerializeField] TextMeshProUGUI replayLabel;
        RankingBattleHistoryEntry currentEntry;
        System.Action<string> replayRequested;

        void Awake() {
            if (replayEmitter != null) {
                replayEmitter.OnClickEvent.Subscribe(_ => {
                    if (currentEntry != null && currentEntry.HasReplay) {
                        replayRequested?.Invoke(currentEntry.Id);
                    }
                }).AddTo(this);
            }
        }

        public void ConfigureCell(RankingBattleHistoryEntry entry, System.Action<string> replayRequested) {
            currentEntry = entry;
            this.replayRequested = replayRequested;
            var replayText = entry.HasReplay ? "  [Replay]" : "";
            lineLabel.text = $"{entry.PlayerAName} vs {entry.PlayerBName}  {entry.ResultText}{replayText}\n{entry.BattleText}  {entry.PlayedAtText}";
            if (replayLabel != null) {
                replayLabel.text = entry.HasReplay ? "Replay" : "";
            }
        }
    }
}
