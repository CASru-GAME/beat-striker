using R3;
using TMPro;
using UnityEngine;

namespace Alice {
    public record RankingPlayerEntry(string Name, int Grade, string AcquiredAt);
    public class RankingPlayerRowView : MonoBehaviour {
        [SerializeField] ActionEmitter emitter;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text dateLabel;
        string playerName;
        System.Action<string> selected;
        void Awake() { emitter.OnClickEvent.Subscribe(_ => selected?.Invoke(playerName)).AddTo(this); }
        public void Set(RankingPlayerEntry entry, System.Action<string> onSelected) {
            playerName = entry.Name; selected = onSelected;
            nameLabel.richText = false; nameLabel.text = entry.Name; dateLabel.text = entry.AcquiredAt;
        }
    }
}
