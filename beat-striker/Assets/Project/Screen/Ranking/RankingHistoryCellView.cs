using PolyAndCode.UI;
using TMPro;
using UnityEngine;

namespace Alice {
    public class RankingHistoryCellView : MonoBehaviour, ICell {
        [SerializeField] TextMeshProUGUI lineLabel;

        public void ConfigureCell(RankingBattleHistoryEntry entry) {
            lineLabel.text = $"{entry.PlayerAName} vs {entry.PlayerBName}  {entry.PlayedAtText}";
        }
    }
}
