using TMPro;
using UnityEngine;

namespace Alice {
    public class BattleIdentityView : MonoBehaviour {
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text playerLabel;
        [SerializeField] GradeBadgeView gradeView;

        public void Set(string playerName, int grade, string playerLabelText) {
            gameObject.SetActive(!string.IsNullOrEmpty(playerName));
            nameLabel.text = playerName;
            playerLabel.text = playerLabelText;
            gradeView.Set(grade);
        }
    }
}
