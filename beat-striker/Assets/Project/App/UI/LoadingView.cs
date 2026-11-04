using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Alice {
    public class LoadingView : MonoBehaviour {
        [Header("Root")]
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Message")]
        [SerializeField] string defaultMessage = "データ読み込み中";

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

        Vector3[] basePositions;
        int activeLoadingCharacterCount;
        double bounceStartTime;
        int fadeTweenId = -1;
        TaskCompletionSource<bool> hideTaskSource;
        string currentMessage;
        bool isVisible;
        bool initialized;

        public bool IsVisible => isVisible;
        public float ShowDelaySeconds => showDelaySeconds;

        public string ResolveDisplayMessage(string message) {
            return string.IsNullOrEmpty(message) ? defaultMessage : message;
        }

        public void SetMessage(string message) {
            EnsureInitialized();
            var displayMessage = ResolveDisplayMessage(message);
            if (currentMessage == displayMessage) {
                return;
            }

            ApplyMessageToCharacters(displayMessage);
            bounceStartTime = Time.realtimeSinceStartupAsDouble;
        }

        void Awake() {
            EnsureInitialized();
        }

        public Task ShowAsync() {
            EnsureInitialized();
            if (isVisible) {
                return Task.CompletedTask;
            }

            isVisible = true;
            canvasGroup.gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            bounceStartTime = Time.realtimeSinceStartupAsDouble;
            if (fadeTweenId >= 0) {
                LeanTween.cancel(fadeTweenId);
                fadeTweenId = -1;
            }
            fadeTweenId = LeanTween.alphaCanvas(canvasGroup, 1f, fadeInDuration)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeOutQuad)
                .id;
            CompleteHideTask(hideTaskSource);
            return Task.CompletedTask;
        }

        public Task HideAsync() {
            EnsureInitialized();
            if (!isVisible) {
                return hideTaskSource?.Task ?? Task.CompletedTask;
            }

            isVisible = false;
            ResetCharacterPositions();
            if (fadeTweenId >= 0) {
                LeanTween.cancel(fadeTweenId);
                fadeTweenId = -1;
            }
            var taskSource = new TaskCompletionSource<bool>();
            hideTaskSource = taskSource;
            fadeTweenId = LeanTween.alphaCanvas(canvasGroup, 0f, fadeOutDuration)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeInQuad)
                .setOnComplete(() => {
                    fadeTweenId = -1;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                    canvasGroup.gameObject.SetActive(false);
                    CompleteHideTask(taskSource);
                })
                .id;
            return taskSource.Task;
        }

        void OnDisable() {
            isVisible = false;
            ResetCharacterPositions();
            if (fadeTweenId >= 0) {
                LeanTween.cancel(fadeTweenId);
                fadeTweenId = -1;
            }
            CompleteHideTask(hideTaskSource);
        }

        void CompleteHideTask(TaskCompletionSource<bool> taskSource) {
            if (hideTaskSource == taskSource) {
                hideTaskSource = null;
            }
            taskSource?.TrySetResult(true);
        }

        void EnsureInitialized() {
            if (initialized) {
                return;
            }

            CacheBasePositions();
            ApplyMessageToCharacters(defaultMessage);
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
            for (var i = 0; i < activeLoadingCharacterCount; i++) {
                loadingCharacters[i].rectTransform.localPosition = basePositions[i];
            }
        }

        void ApplyMessageToCharacters(string message) {
            currentMessage = message;
            var maxSlots = loadingCharacters.Length;
            var textIndex = 0;
            var elementIndex = 0;
            while (textIndex < message.Length && elementIndex < maxSlots) {
                if (char.IsHighSurrogate(message, textIndex) && textIndex + 1 < message.Length && char.IsLowSurrogate(message, textIndex + 1)) {
                    loadingCharacters[elementIndex].gameObject.SetActive(true);
                    loadingCharacters[elementIndex].text = message.Substring(textIndex, 2);
                    textIndex += 2;
                }
                else {
                    loadingCharacters[elementIndex].gameObject.SetActive(true);
                    loadingCharacters[elementIndex].text = message[textIndex].ToString();
                    textIndex += 1;
                }

                elementIndex += 1;
            }

            activeLoadingCharacterCount = elementIndex;
            for (var i = elementIndex; i < maxSlots; i++) {
                loadingCharacters[i].text = "";
                loadingCharacters[i].gameObject.SetActive(false);
            }

            for (var i = 0; i < elementIndex; i++) {
                loadingCharacters[i].rectTransform.localPosition = basePositions[i];
            }
        }

        void Update() {
            if (!isVisible || activeLoadingCharacterCount == 0) {
                return;
            }

            var duration = Mathf.Max(0.01f, bounceDuration);
            var cycleDuration = duration * 2f + characterDelay * (activeLoadingCharacterCount - 1) + loopDelay;
            // 実時間から全ての文字の位相を求め、重いフレームでも遅延を次のループへ持ち越さない。
            var cycleTime = (Time.realtimeSinceStartupAsDouble - bounceStartTime) % cycleDuration;
            for (var i = 0; i < activeLoadingCharacterCount; i++) {
                var characterTime = (float)cycleTime - characterDelay * i;
                var height = 0f;
                if (characterTime >= 0f && characterTime < duration * 2f) {
                    var phase = characterTime / duration - 1f;
                    height = bounceHeight * (1f - phase * phase);
                }

                loadingCharacters[i].rectTransform.localPosition = basePositions[i] + Vector3.up * height;
            }
        }

        void ResetCharacterPositions() {
            for (var i = 0; i < activeLoadingCharacterCount; i++) {
                loadingCharacters[i].rectTransform.localPosition = basePositions[i];
            }
        }
    }
}
