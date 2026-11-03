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
        TextMeshProUGUI playerALabel, playerBLabel, resultLabel, detailLabel, dateLabel;
        RankingSlantedGraphic replayPlate;
        RectTransform art;
        RankingBattleHistoryEntry currentEntry;
        System.Action<string> replayRequested;

        void Awake() {
            var font = lineLabel.font;
            if (!displayFont) displayFont = font;
            lineLabel.gameObject.SetActive(false);
            var background = GetComponent<Image>(); background.color = Color.clear; background.sprite = null;
            art = RankingUi.Rect(transform, "Match Strip", Vector2.zero, Vector2.one, new Vector2(7, 8), new Vector2(-7, -5));
            RankingUi.Band(art, "White Edge", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white, Color.white, 15);
            RankingUi.Band(art, "Black Frame", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4), RankingUi.Ink, RankingUi.Ink, 15);
            RankingUi.Band(art, "Player One", new Vector2(0, 0.29f), new Vector2(0.51f, 1), new Vector2(10, 0), new Vector2(-5, -8), RankingUi.Cyan, new Color32(0, 139, 208, 255), 12);
            RankingUi.Band(art, "Player Two", new Vector2(0.49f, 0.29f), new Vector2(1, 1), new Vector2(5, 0), new Vector2(-10, -8), RankingUi.Pink, new Color32(195, 30, 103, 255), 12);
            RankingUi.Text(art, "P1", displayFont, "1P", new Vector2(0.03f, 0.69f), new Vector2(0.2f, 0.94f), Vector2.zero, Vector2.zero, 19, RankingUi.Ink);
            RankingUi.Text(art, "P2", displayFont, "2P", new Vector2(0.81f, 0.69f), new Vector2(0.97f, 0.94f), Vector2.zero, Vector2.zero, 19, RankingUi.Ink, TextAlignmentOptions.MidlineRight);
            playerALabel = RankingUi.Text(art, "Player One Name", font, "", new Vector2(0.04f, 0.35f), new Vector2(0.43f, 0.75f), Vector2.zero, Vector2.zero, 25, Color.white);
            playerBLabel = RankingUi.Text(art, "Player Two Name", font, "", new Vector2(0.57f, 0.35f), new Vector2(0.96f, 0.75f), Vector2.zero, Vector2.zero, 25, Color.white, TextAlignmentOptions.MidlineRight);
            RankingUi.Band(art, "Versus Slash", new Vector2(0.435f, 0.29f), new Vector2(0.565f, 1), new Vector2(-5, -1), new Vector2(5, 0), RankingUi.Ink, RankingUi.Ink, 12);
            var versus = RankingUi.Text(art, "Versus", displayFont, "VS", new Vector2(0.425f, 0.32f), new Vector2(0.575f, 0.86f), Vector2.zero, Vector2.zero, 34, Color.white, TextAlignmentOptions.Center);
            versus.rectTransform.localEulerAngles = new Vector3(0, 0, 8);
            resultLabel = RankingUi.Text(art, "Winner", displayFont, "", new Vector2(0.2f, 0.73f), new Vector2(0.8f, 1), Vector2.zero, new Vector2(0, -5), 18, RankingUi.Yellow, TextAlignmentOptions.Center);
            dateLabel = RankingUi.Text(art, "Date", font, "", new Vector2(0.025f, 0.02f), new Vector2(0.34f, 0.29f), new Vector2(4, 1), Vector2.zero, 11, Color.white);
            detailLabel = RankingUi.Text(art, "Match Detail", font, "", new Vector2(0.34f, 0.02f), new Vector2(0.81f, 0.29f), Vector2.zero, new Vector2(-5, 0), 11, Color.white);
            replayPlate = RankingUi.Band(art, "Replay Plate", new Vector2(0.81f, 0.02f), new Vector2(0.975f, 0.29f), Vector2.zero, Vector2.zero, RankingUi.Yellow, RankingUi.Yellow, 6);
            RankingUi.Text(replayPlate.transform, "Replay", displayFont, "リプレイ", Vector2.zero, Vector2.one, new Vector2(3, 0), new Vector2(-3, 0), 14, RankingUi.Ink, TextAlignmentOptions.Center);
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
