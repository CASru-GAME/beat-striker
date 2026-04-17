using System.Threading.Tasks;
using UnityEngine;

namespace Alice {
    public class BattleFadeView : MonoBehaviour {
        [SerializeField] CanvasGroup fadePanel;
        [SerializeField] float fadePanelDuration = 0.5f;

        TaskCompletionSource<bool> fadeInCompletionSource;
        TaskCompletionSource<bool> fadeOutCompletionSource;

        void Awake() {
            fadePanel.alpha = 1f;
            fadePanel.gameObject.SetActive(true);
        }

        public Task PresentFadeInAsync() {
            fadeInCompletionSource?.TrySetCanceled();
            fadeInCompletionSource = new TaskCompletionSource<bool>();
            LeanTween.cancel(fadePanel.gameObject);
            fadePanel.gameObject.SetActive(true);

            LeanTween.alphaCanvas(fadePanel, 1f, fadePanelDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnComplete(() => {
                    fadeInCompletionSource?.TrySetResult(true);
                });

            return fadeInCompletionSource.Task;
        }

        public Task PresentFadeOutAsync() {
            fadeOutCompletionSource?.TrySetCanceled();
            fadeOutCompletionSource = new TaskCompletionSource<bool>();
            LeanTween.cancel(fadePanel.gameObject);
            fadePanel.gameObject.SetActive(true);

            LeanTween.alphaCanvas(fadePanel, 0f, fadePanelDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnComplete(() => {
                    fadePanel.gameObject.SetActive(false);
                    fadeOutCompletionSource?.TrySetResult(true);
                });

            return fadeOutCompletionSource.Task;
        }
    }
}