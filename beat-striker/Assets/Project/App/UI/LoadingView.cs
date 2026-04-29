using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Alice {
    public class LoadingView : MonoBehaviour {
        [Header("Root")]
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Characters")]
        [SerializeField] TextMeshProUGUI[] loadingCharacters;

        [Header("Fade")]
        [SerializeField, Min(0f)] float fadeInDuration = 0.18f;
        [SerializeField, Min(0f)] float fadeOutDuration = 0.15f;
        [SerializeField, Min(0f)] float showDelaySeconds = 0.3f;

        [Header("Bounce")]
        [SerializeField, Min(0f)] float bounceHeight = 14f;
        [SerializeField, Min(0.01f)] float bounceDuration = 0.22f;
        [SerializeField, Min(0f)] float characterDelay = 0.05f;
        [SerializeField, Min(0f)] float loopDelay = 0.1f;

        readonly TaskCompletionSource<bool> completedTaskSource = new();
        Vector3[] basePositions;
        int loopTweenId = -1;
        int fadeTweenId = -1;
        bool isVisible;
        bool initialized;

        public bool IsVisible => isVisible;
        public float ShowDelaySeconds => showDelaySeconds;

        void Awake() {
            EnsureInitialized();
            completedTaskSource.TrySetResult(true);
        }

        public Task ShowAsync() {
            EnsureInitialized();
            if (isVisible) {
                return completedTaskSource.Task;
            }

            isVisible = true;
            canvasGroup.gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            StartBounceLoop();
            if (fadeTweenId >= 0) {
                LeanTween.cancel(fadeTweenId);
                fadeTweenId = -1;
            }
            fadeTweenId = LeanTween.alphaCanvas(canvasGroup, 1f, fadeInDuration)
                .setEase(LeanTweenType.easeOutQuad)
                .id;
            return completedTaskSource.Task;
        }

        public Task HideAsync() {
            EnsureInitialized();
            if (!isVisible) {
                return completedTaskSource.Task;
            }

            isVisible = false;
            StopBounceLoop();
            if (fadeTweenId >= 0) {
                LeanTween.cancel(fadeTweenId);
                fadeTweenId = -1;
            }
            var tcs = new TaskCompletionSource<bool>();
            fadeTweenId = LeanTween.alphaCanvas(canvasGroup, 0f, fadeOutDuration)
                .setEase(LeanTweenType.easeInQuad)
                .setOnComplete(() => {
                    fadeTweenId = -1;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                    canvasGroup.gameObject.SetActive(false);
                    tcs.TrySetResult(true);
                })
                .id;
            return tcs.Task;
        }

        void OnDisable() {
            StopBounceLoop();
        }

        void OnDestroy() {
            StopBounceLoop();
        }

        void EnsureInitialized() {
            if (initialized) {
                return;
            }

            CacheBasePositions();
            ApplyHiddenImmediately();
            initialized = true;
        }

        void CacheBasePositions() {
            basePositions = new Vector3[loadingCharacters.Length];
            for (var i = 0; i < loadingCharacters.Length; i++) {
                basePositions[i] = loadingCharacters[i].rectTransform.localPosition;
            }
        }

        void ApplyHiddenImmediately() {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.gameObject.SetActive(false);
            for (var i = 0; i < loadingCharacters.Length; i++) {
                loadingCharacters[i].rectTransform.localPosition = basePositions[i];
            }
        }

        void StartBounceLoop() {
            StopBounceLoop();
            for (var i = 0; i < loadingCharacters.Length; i++) {
                ScheduleCharacterBounce(i);
            }
        }

        void ScheduleCharacterBounce(int index) {
            var delay = characterDelay * index;
            loopTweenId = LeanTween.delayedCall(gameObject, delay, () => {
                if (!isVisible) {
                    return;
                }

                PlayBounce(index);
            }).id;
        }

        void PlayBounce(int index) {
            var target = loadingCharacters[index].rectTransform;
            var basePosition = basePositions[index];
            var peakPosition = basePosition + new Vector3(0f, bounceHeight, 0f);
            LeanTween.value(gameObject, 0f, 1f, bounceDuration)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnUpdate((float value) => {
                    target.localPosition = Vector3.Lerp(basePosition, peakPosition, value);
                })
                .setOnComplete(() => {
                    LeanTween.value(gameObject, 0f, 1f, bounceDuration)
                        .setEase(LeanTweenType.easeInQuad)
                        .setOnUpdate((float value) => {
                            target.localPosition = Vector3.Lerp(peakPosition, basePosition, value);
                        })
                        .setOnComplete(() => {
                            if (!isVisible) {
                                target.localPosition = basePosition;
                                return;
                            }

                            if (index == loadingCharacters.Length - 1) {
                                loopTweenId = LeanTween.delayedCall(gameObject, loopDelay, StartBounceLoop).id;
                            }
                        });
                });
        }

        void StopBounceLoop() {
            if (loopTweenId >= 0) {
                LeanTween.cancel(loopTweenId);
                loopTweenId = -1;
            }
            LeanTween.cancel(gameObject);

            if (basePositions == null) {
                return;
            }

            for (var i = 0; i < loadingCharacters.Length; i++) {
                loadingCharacters[i].rectTransform.localPosition = basePositions[i];
            }
        }
    }
}
