using System;
using Core;
using R3;
using TMPro;
using UnityEngine;

namespace Alice {
    public class OnlineRoomPlayerView : MonoBehaviour {
        [SerializeField] Botan button;
        [SerializeField] TextMeshProUGUI numberLabel;
        [SerializeField] TextMeshProUGUI nameLabel;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField] GradeBadgeView gradeView;
        [SerializeField] GameObject selectionMark;
        [SerializeField] RankingSlantedGraphic plate;
        [SerializeField] RectTransform art;
        Action<string> onSelect;
        string sessionId;

        void Awake() {
            button.OnClickEvent.Subscribe(_ => onSelect?.Invoke(sessionId)).AddTo(this);
            button.OnHoverEvent.Subscribe(_ => art.localScale = new Vector3(1.025f, 1.025f, 1)).AddTo(this);
            button.OnHoverExitEvent.Subscribe(_ => art.localScale = Vector3.one).AddTo(this);
        }

        public void Bind(OnlineRoomPlayer player, int index, bool selected, Action<string> select) {
            sessionId = player.sessionId;
            onSelect = select;
            numberLabel.text = (index + 1).ToString("00");
            nameLabel.richText = false;
            nameLabel.text = player.player == null ? OnlineRoomView.PlayerName(sessionId) : player.player.name;
            statusLabel.text = player.canInvite ? "対戦OK" : "取り込み中";
            gradeView.gameObject.SetActive(player.player != null);
            if (player.player != null) gradeView.Set(player.player.grade);
            selectionMark.SetActive(selected);
            var color = selected ? new Color32(255, 220, 0, 255)
                : player.canInvite ? new Color32(0, 206, 224, 255) : new Color32(90, 95, 104, 255);
            plate.color = plate.endColor = color;
            art.localScale = Vector3.one;
        }

        void OnDisable() {
            if (art) art.localScale = Vector3.one;
        }
    }
}
