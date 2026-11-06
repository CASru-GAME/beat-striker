using TMPro;
using UnityEngine;

namespace Alice {
    public class TrialIntroductionView : MonoBehaviour {
        [SerializeField] ChoiceDialogView dialog;
        [SerializeField] CanvasGroup[] tiers;
        [SerializeField] TMP_Text[] tierLabels;
        [SerializeField] AudioSource narration;
        [SerializeField] AudioClip[] narrationClips;
        public ChoiceDialogView Dialog => dialog;
        public void SetVolume(float volume) => narration.volume = volume;
        public void ShowPage() {
            for (var i = 0; i < tiers.Length; i++) {
                tiers[i].alpha = 1f;
                tierLabels[i].text = PlayerGrades.Label((PlayerGrade)i);
            }
            narration.Stop();
            narration.clip = narrationClips[0];
            narration.Play();
        }
        public void StopNarration() => narration.Stop();
        void OnDisable() => StopNarration();
    }
}
