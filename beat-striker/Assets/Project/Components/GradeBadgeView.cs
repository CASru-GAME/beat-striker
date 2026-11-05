using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class GradeBadgeView : MonoBehaviour {
        [SerializeField] Image badge;
        [SerializeField] TMP_Text label;
        [SerializeField] Sprite[] badges;

        public void Set(int grade) {
            var known = grade >= 0 && grade < badges.Length;
            badge.gameObject.SetActive(known);
            if (known) badge.sprite = badges[grade];
            label.text = PlayerGrades.Label((PlayerGrade)grade);
        }
    }
}
