using PolyAndCode.UI;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class RankingHistoryCellView : MonoBehaviour, ICell {
        [SerializeField] TextMeshProUGUI lineLabel;
        [SerializeField] ActionEmitter replayEmitter;
        [SerializeField] TMP_FontAsset displayFont;
        [SerializeField] TextMeshProUGUI playerALabel, playerBLabel, resultLabel, detailLabel, dateLabel;
        [SerializeField] RankingSlantedGraphic replayPlate;
        [SerializeField] RectTransform art;
        RankingBattleHistoryEntry currentEntry;
        System.Action<string> replayRequested;

        void Awake() {
            replayEmitter.OnClickEvent.Subscribe(_ => {
                if (currentEntry != null && currentEntry.HasReplay && !string.IsNullOrWhiteSpace(currentEntry.Id)) replayRequested?.Invoke(currentEntry.Id);
            }).AddTo(this);
            replayEmitter.OnHoverEvent.Subscribe(_ => SetHovered(true)).AddTo(this);
            replayEmitter.OnHoverExitEvent.Subscribe(_ => SetHovered(false)).AddTo(this);
        }

        void SetHovered(bool value) {
            var hovered = value && currentEntry != null && currentEntry.HasReplay;
            art.localScale = hovered ? new Vector3(1.015f, 1.025f, 1) : Vector3.one;
            replayPlate.color = replayPlate.endColor = hovered ? Color.white : RankingUi.Yellow;
            replayPlate.SetVerticesDirty();
        }

        public void ConfigureCell(RankingBattleHistoryEntry entry, System.Action<string> onReplayRequested) {
            currentEntry = entry; replayRequested = onReplayRequested;
            playerALabel.text = entry.PlayerAName; playerBLabel.text = entry.PlayerBName;
            playerALabel.richText = playerBLabel.richText = false;
            resultLabel.text = entry.ResultText;
            detailLabel.text = entry.BattleText; detailLabel.richText = false;
            dateLabel.text = entry.PlayedAtText;
            replayPlate.gameObject.SetActive(entry.HasReplay);
            SetHovered(false);
        }

        void OnDisable() {
            if (art) SetHovered(false);
        }
    }
}
