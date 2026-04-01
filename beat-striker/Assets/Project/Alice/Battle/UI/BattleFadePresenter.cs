using R3;
using UnityEngine;

namespace Alice {
    public class BattleFadePresenter : MonoBehaviour {
        [SerializeField] CanvasGroup fadePanel;
        [SerializeField] float fadePanelDuration = 0.5f;
        [SerializeField] float fadeHoldDuration = 0.5f;

        readonly Subject<Unit> fadeInCompletedSubject = new();
        public Observable<Unit> FadeInCompleted => fadeInCompletedSubject;

        void Awake() {
            fadePanel.alpha = 0f;
            fadePanel.gameObject.SetActive(false);
        }

        public void PresentFadeTransition() {
            fadePanel.gameObject.SetActive(true);
            fadePanel.alpha = 0f;

            LeanTween.alphaCanvas(fadePanel, 1f, fadePanelDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnComplete(() => {
                    fadeInCompletedSubject.OnNext(Unit.Default);

                    LeanTween.delayedCall(fadeHoldDuration, () => {
                        LeanTween.alphaCanvas(fadePanel, 0f, fadePanelDuration)
                            .setEase(LeanTweenType.easeInOutQuad)
                            .setOnComplete(() => {
                                fadePanel.gameObject.SetActive(false);
                            });
                    });
                });
        }
    }
}