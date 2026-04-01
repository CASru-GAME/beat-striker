using R3;
using TMPro;
using UnityEngine;

namespace Alice {
    public class BattleRoundStartPresenter : MonoBehaviour {
        [SerializeField] TextMeshProUGUI battleStartText;
        [SerializeField] TextMeshProUGUI roundNumberText;
        [SerializeField] CanvasGroup roundNumberCanvasGroup;
        [SerializeField] AudioClip roundSound;
        [SerializeField] AudioClip fightSound;
        [SerializeField] float soundVolume = 1.0f;

        [Header("Animation Timing")]
        [SerializeField] float roundFadeInDuration = 0.5f;
        [SerializeField] float roundDisplayDuration = 0.5f;
        [SerializeField] float roundFadeOutDuration = 0.5f;
        [SerializeField] float delayBeforeFight = 0.2f;
        [SerializeField] float fightScaleDuration = 0.2f;
        [SerializeField] float fightDisplayDuration = 0.7f;
        [SerializeField] float delayBeforeRoundText = 0.5f;

        readonly Subject<Unit> animationFinishedSubject = new();
        public Observable<Unit> AnimationFinished => animationFinishedSubject;

        void Awake() {
            battleStartText.gameObject.SetActive(false);
            roundNumberText.gameObject.SetActive(false);
        }

        public void PresentRoundStart(int roundNumber) {
            LeanTween.delayedCall(delayBeforeRoundText, () => ShowRoundText(roundNumber));
        }

        void ShowRoundText(int roundNumber) {
            roundNumberText.text = $"Round {roundNumber}";
            roundNumberText.gameObject.SetActive(true);
            PlaySound(roundSound);

            roundNumberCanvasGroup.alpha = 0f;
            LeanTween.alphaCanvas(roundNumberCanvasGroup, 1f, roundFadeInDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnComplete(() => {
                    LeanTween.alphaCanvas(roundNumberCanvasGroup, 0f, roundFadeOutDuration)
                        .setDelay(roundDisplayDuration)
                        .setEase(LeanTweenType.easeInOutQuad)
                        .setOnComplete(() => {
                            roundNumberText.gameObject.SetActive(false);
                            LeanTween.delayedCall(delayBeforeFight, ShowFightText);
                        });
                });
        }

        void ShowFightText() {
            battleStartText.text = "Fight!";
            battleStartText.gameObject.SetActive(true);
            PlaySound(fightSound);
            battleStartText.transform.localScale = Vector3.one * 10f;

            LeanTween.scale(battleStartText.gameObject, Vector3.one, fightScaleDuration)
                .setEase(LeanTweenType.easeOutQuad);

            LeanTween.delayedCall(fightDisplayDuration, () => {
                battleStartText.gameObject.SetActive(false);
                animationFinishedSubject.OnNext(Unit.Default);
            });
        }

        void PlaySound(AudioClip clip) {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, Vector3.zero, soundVolume);
        }
    }
}