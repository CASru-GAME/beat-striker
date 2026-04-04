using TMPro;
using UnityEngine;

namespace Alice {
    public class AliceComboView : MonoBehaviour {
        [SerializeField] TextMeshProUGUI comboText;

        int previousComboCount;

        void Awake() {
            comboText.gameObject.SetActive(false);
            comboText.transform.localScale = Vector3.one;
        }

        public void SetComboCount(int comboCount) {
            var currentComboCount = Mathf.Max(0, comboCount);

            if (currentComboCount > 0) {
                if (!comboText.gameObject.activeSelf) {
                    comboText.gameObject.SetActive(true);
                }

                if (currentComboCount > previousComboCount) {
                    PlayComboAnimation();
                }

                comboText.text = currentComboCount.ToString();
            } else {
                if (comboText.gameObject.activeSelf) {
                    comboText.gameObject.SetActive(false);
                }
                comboText.transform.localScale = Vector3.one;
            }

            previousComboCount = currentComboCount;
        }

        void PlayComboAnimation() {
            LeanTween.cancel(comboText.gameObject);
            LeanTween.scale(comboText.gameObject, Vector3.one * 1.15f, 0.1f)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() => {
                    LeanTween.scale(comboText.gameObject, Vector3.one, 0.1f)
                        .setEase(LeanTweenType.easeInQuad);
                });
        }
    }
}
