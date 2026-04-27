using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Alice {
    public enum TutorialUiKey {
        Overview,
        WalkDescription,
        WalkPractice,
        DashDescription,
        DashPractice,
        AttackDescription,
        AttackPractice,
        ChargeDescription,
        ChargePractice,
        ChargeAttackDescription,
        ChargeAttackPractice,
        GuardDescription,
        GuardPractice,
        HpDescription,
        SpDescription,
        SpecialDescription,
        SpecialPractice,
        Final,
    }

    public class BattleTutorialView : MonoBehaviour {
        [Header("Overview")]
        [SerializeField] GameObject overviewPanel;

        [Header("Canvas")]
        [SerializeField] CanvasGroup blackPanel;
        [SerializeField] float blackPanelFadeDuration = 0.18f;

        [Header("Success")]
        [SerializeField] CanvasGroup successPanel;
        [SerializeField] AudioClip successSound;

        [Header("Success Motion")]
        [SerializeField, Min(0f)] float successTravelDistanceFactor = 0.5f;
        [SerializeField, Min(0f)] float successTravelPadding = 120f;
        [SerializeField, Min(0f)] float successEnterSeconds = 0.2f;
        [SerializeField, Min(0f)] float successHoldSeconds = 0.35f;
        [SerializeField, Min(0f)] float successExitSeconds = 0.2f;

        [Header("Sound")]
        [SerializeField] AudioClip descriptionChangeSound;
        [SerializeField] AudioClip practiceStartSound;

        [Header("Hp")]
        [SerializeField] GameObject hpDescriptionPanel;
        [SerializeField] GameObject hpBarHighlightObject;

        [Header("Sp")]
        [SerializeField] GameObject spDescriptionPanel;
        [SerializeField] GameObject spBarHighlightObject;

        [Header("Walk")]
        [SerializeField] GameObject walkDescriptionPanel;
        [SerializeField] GameObject walkPracticePanel;
        [SerializeField] float walkPracticeSuccessDisplaySeconds = 0.8f;
        [SerializeField] float walkVelocityThreshold = 0.2f;
        [SerializeField, Min(0f)] float walkPracticeRequiredTravelDistance = 1.5f;

        [Header("Dash")]
        [SerializeField] GameObject dashDescriptionPanel;
        [SerializeField] GameObject dashPracticePanel;
        [SerializeField] float dashPracticeSuccessDisplaySeconds = 0.8f;

        [Header("Attack")]
        [SerializeField] GameObject attackDescriptionPanel;
        [SerializeField] GameObject attackPracticePanel;
        [SerializeField] float attackPracticeSuccessDisplaySeconds = 0.8f;

        [Header("Charge")]
        [SerializeField] GameObject chargeDescriptionPanel;
        [SerializeField] GameObject chargePracticePanel;
        [SerializeField] float chargePracticeSuccessDisplaySeconds = 0.8f;

        [Header("Charge Attack")]
        [SerializeField] GameObject chargeAttackDescriptionPanel;
        [SerializeField] GameObject chargeAttackPracticePanel;
        [SerializeField] float chargeAttackPracticeSuccessDisplaySeconds = 0.8f;

        [Header("Guard")]
        [SerializeField] GameObject guardDescriptionPanel;
        [SerializeField] GameObject guardPracticePanel;
        [SerializeField] float guardPracticeSuccessDisplaySeconds = 0.8f;

        [Header("Special")]
        [SerializeField] GameObject specialDescriptionPanel;
        [SerializeField] GameObject specialPracticePanel;
        [SerializeField] float specialPracticeSuccessDisplaySeconds = 0.8f;

        [Header("Final")]
        [SerializeField] GameObject finalPanel;
        [SerializeField] float finalPanelDisplaySeconds = 0.8f;

        readonly Dictionary<TutorialUiKey, GameObject> panelMap = new();
        bool initialized;
        bool hasCurrentShownKey;
        TutorialUiKey currentShownKey;
        int canvasTransitionToken;
        TaskCompletionSource<bool> canvasTransitionCompletionSource;
        RectTransform successPanelRect;
        Vector2 successPanelRestAnchoredPosition;
        int successTransitionToken;
        TaskCompletionSource<bool> successTransitionCompletionSource;
        bool successRuntimeInitialized;

        public float WalkVelocityThreshold => walkVelocityThreshold;
        public float WalkPracticeRequiredTravelDistance => walkPracticeRequiredTravelDistance;

        void Awake() {
            EnsureInitialized();
            EnsureSuccessRuntimeInitialized();
            HideAllPanels();
            ResetBlackStateImmediate();
        }

        public Task ShowAsync(TutorialUiKey key) {
            EnsureInitialized();
            EnsureSuccessRuntimeInitialized();
            var previousShownKey = hasCurrentShownKey ? currentShownKey : (TutorialUiKey?)null;
            HideAllPanels();
            UpdateHighlightObjects(key);
            if (panelMap.TryGetValue(key, out var panel)) {
                panel.SetActive(true);
            }

            PlayTransitionSound(previousShownKey, key);
            currentShownKey = key;
            hasCurrentShownKey = true;
            return SetBlackVisibleAsync(IsDescriptionKey(key));
        }

        public async Task HideAfterClearDelayAsync(TutorialUiKey practiceKey) {
            await PlaySuccessSequenceAsync();

            var waitSeconds = GetPracticeSuccessWaitSeconds(practiceKey);
            if (waitSeconds > 0f) {
                await DelayAsync(waitSeconds);
            }

            HideAllPanels();
            await SetBlackVisibleAsync(false);
        }

        public async Task HideAfterFinalDelayAsync() {
            if (finalPanelDisplaySeconds > 0f) {
                await DelayAsync(finalPanelDisplaySeconds);
            }

            HideAllPanels();
            await SetBlackVisibleAsync(false);
        }

        public void HideAll() {
            EnsureInitialized();
            HideAllPanels();
            _ = SetBlackVisibleAsync(false);
        }

        void EnsureInitialized() {
            if (initialized) {
                return;
            }

            panelMap.Clear();
            RegisterPanel(TutorialUiKey.Overview, overviewPanel);
            RegisterPanel(TutorialUiKey.WalkDescription, walkDescriptionPanel);
            RegisterPanel(TutorialUiKey.WalkPractice, walkPracticePanel);
            RegisterPanel(TutorialUiKey.DashDescription, dashDescriptionPanel);
            RegisterPanel(TutorialUiKey.DashPractice, dashPracticePanel);
            RegisterPanel(TutorialUiKey.AttackDescription, attackDescriptionPanel);
            RegisterPanel(TutorialUiKey.AttackPractice, attackPracticePanel);
            RegisterPanel(TutorialUiKey.ChargeDescription, chargeDescriptionPanel);
            RegisterPanel(TutorialUiKey.ChargePractice, chargePracticePanel);
            RegisterPanel(TutorialUiKey.ChargeAttackDescription, chargeAttackDescriptionPanel);
            RegisterPanel(TutorialUiKey.ChargeAttackPractice, chargeAttackPracticePanel);
            RegisterPanel(TutorialUiKey.GuardDescription, guardDescriptionPanel);
            RegisterPanel(TutorialUiKey.GuardPractice, guardPracticePanel);
            RegisterPanel(TutorialUiKey.HpDescription, hpDescriptionPanel);
            RegisterPanel(TutorialUiKey.SpDescription, spDescriptionPanel);
            RegisterPanel(TutorialUiKey.SpecialDescription, specialDescriptionPanel);
            RegisterPanel(TutorialUiKey.SpecialPractice, specialPracticePanel);
            RegisterPanel(TutorialUiKey.Final, finalPanel);

            initialized = true;
            EnsureSuccessRuntimeInitialized();
        }

        void EnsureSuccessRuntimeInitialized() {
            if (successRuntimeInitialized) {
                return;
            }

            successPanelRect = (RectTransform)successPanel.transform;
            successPanelRestAnchoredPosition = successPanelRect.anchoredPosition;
            successRuntimeInitialized = true;
        }

        void RegisterPanel(TutorialUiKey key, GameObject panelRoot) {
            if (panelRoot == null) {
                return;
            }

            panelMap[key] = panelRoot;
        }

        void HideAllPanels() {
            foreach (var panel in panelMap.Values) {
                panel.SetActive(false);
            }

            SetHighlightObjectsActive(false);
            ResetSuccessStateImmediate();
        }

        void UpdateHighlightObjects(TutorialUiKey key) {
            hpBarHighlightObject.SetActive(key == TutorialUiKey.HpDescription);
            spBarHighlightObject.SetActive(key == TutorialUiKey.SpDescription || key == TutorialUiKey.SpecialDescription);
        }

        void SetHighlightObjectsActive(bool isActive) {
            hpBarHighlightObject.SetActive(isActive);
            spBarHighlightObject.SetActive(isActive);
        }

        float GetPracticeSuccessWaitSeconds(TutorialUiKey practiceKey) {
            return practiceKey switch {
                TutorialUiKey.WalkPractice => walkPracticeSuccessDisplaySeconds,
                TutorialUiKey.DashPractice => dashPracticeSuccessDisplaySeconds,
                TutorialUiKey.AttackPractice => attackPracticeSuccessDisplaySeconds,
                TutorialUiKey.ChargePractice => chargePracticeSuccessDisplaySeconds,
                TutorialUiKey.ChargeAttackPractice => chargeAttackPracticeSuccessDisplaySeconds,
                TutorialUiKey.GuardPractice => guardPracticeSuccessDisplaySeconds,
                TutorialUiKey.SpecialPractice => specialPracticeSuccessDisplaySeconds,
                _ => 0f,
            };
        }

        async Task PlaySuccessSequenceAsync() {
            var totalSeconds = GetSuccessTotalSeconds();
            if (totalSeconds <= 0f) {
                return;
            }

            var completionSource = new TaskCompletionSource<bool>();
            var token = ++successTransitionToken;

            successTransitionCompletionSource?.TrySetResult(true);
            successTransitionCompletionSource = completionSource;

            LeanTween.cancel(successPanel.gameObject);

            successPanel.gameObject.SetActive(true);
            successPanel.interactable = false;
            successPanel.blocksRaycasts = false;

            var travelDistance = GetSuccessTravelDistance();
            var leftPosition = successPanelRestAnchoredPosition + Vector2.left * travelDistance;
            var rightPosition = successPanelRestAnchoredPosition + Vector2.right * travelDistance;

            successPanelRect.anchoredPosition = leftPosition;
            successPanel.alpha = 0f;

            if (successSound != null) {
                successSound.PlayAtApp(Vector3.zero);
            }

            LeanTween.value(successPanel.gameObject, 0f, totalSeconds, totalSeconds)
                .setEase(LeanTweenType.linear)
                .setOnUpdate((float value) => {
                    if (token != successTransitionToken) {
                        return;
                    }

                    UpdateSuccessMotion(value, leftPosition, rightPosition);
                })
                .setOnComplete(() => {
                    if (token != successTransitionToken) {
                        return;
                    }

                    successPanel.alpha = 0f;
                    successPanelRect.anchoredPosition = successPanelRestAnchoredPosition;
                    successPanel.gameObject.SetActive(false);
                    completionSource.TrySetResult(true);
                });

            await completionSource.Task;
        }

        void UpdateSuccessMotion(float elapsedSeconds, Vector2 leftPosition, Vector2 rightPosition) {
            var enterSeconds = Mathf.Max(0f, successEnterSeconds);
            var holdSeconds = Mathf.Max(0f, successHoldSeconds);
            var exitSeconds = Mathf.Max(0f, successExitSeconds);

            var enterEnd = enterSeconds;
            var holdEnd = enterEnd + holdSeconds;

            if (elapsedSeconds <= enterEnd) {
                var enterProgress = enterSeconds <= 0f ? 1f : elapsedSeconds / enterSeconds;
                var enterEased = EaseOutCubic(enterProgress);
                successPanelRect.anchoredPosition = Vector2.LerpUnclamped(leftPosition, successPanelRestAnchoredPosition, enterEased);
                successPanel.alpha = enterEased;
                return;
            }

            if (elapsedSeconds <= holdEnd) {
                successPanelRect.anchoredPosition = successPanelRestAnchoredPosition;
                successPanel.alpha = 1f;
                return;
            }

            var exitProgress = exitSeconds <= 0f ? 1f : (elapsedSeconds - holdEnd) / exitSeconds;
            var exitEased = EaseInCubic(exitProgress);
            successPanelRect.anchoredPosition = Vector2.LerpUnclamped(successPanelRestAnchoredPosition, rightPosition, exitEased);
            successPanel.alpha = 1f - exitEased;
        }

        float GetSuccessTravelDistance() {
            EnsureSuccessRuntimeInitialized();
            var parentRect = (RectTransform)successPanelRect.parent;
            return parentRect.rect.width * successTravelDistanceFactor + successTravelPadding;
        }

        float GetSuccessTotalSeconds() {
            return Mathf.Max(0f, successEnterSeconds) + Mathf.Max(0f, successHoldSeconds) + Mathf.Max(0f, successExitSeconds);
        }

        static float EaseOutCubic(float t) {
            t = Mathf.Clamp01(t);
            var inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        static float EaseInCubic(float t) {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        void ResetSuccessStateImmediate() {
            EnsureSuccessRuntimeInitialized();
            LeanTween.cancel(successPanel.gameObject);
            successTransitionToken += 1;
            successTransitionCompletionSource?.TrySetResult(true);
            successTransitionCompletionSource = null;

            successPanel.alpha = 0f;
            successPanel.interactable = false;
            successPanel.blocksRaycasts = false;
            successPanelRect.anchoredPosition = successPanelRestAnchoredPosition;
            successPanel.gameObject.SetActive(false);
        }

        Task SetBlackVisibleAsync(bool isVisible) {
            var targetAlpha = isVisible ? 1f : 0f;
            var token = ++canvasTransitionToken;

            canvasTransitionCompletionSource?.TrySetResult(true);
            canvasTransitionCompletionSource = null;

            LeanTween.cancel(blackPanel.gameObject);

            if (blackPanelFadeDuration <= 0f || Mathf.Approximately(blackPanel.alpha, targetAlpha)) {
                blackPanel.alpha = targetAlpha;
                blackPanel.interactable = isVisible;
                blackPanel.blocksRaycasts = isVisible;

                return Task.CompletedTask;
            }

            if (isVisible) {
                blackPanel.gameObject.SetActive(true);
            }

            blackPanel.interactable = isVisible;
            blackPanel.blocksRaycasts = isVisible;

            var completionSource = new TaskCompletionSource<bool>();
            canvasTransitionCompletionSource = completionSource;
            LeanTween.alphaCanvas(blackPanel, targetAlpha, blackPanelFadeDuration)
                .setEase(isVisible ? LeanTweenType.easeOutQuad : LeanTweenType.easeInQuad)
                .setOnComplete(() => {
                    if (token != canvasTransitionToken) {
                        return;
                    }

                    blackPanel.alpha = targetAlpha;
                    blackPanel.interactable = isVisible;
                    blackPanel.blocksRaycasts = isVisible;

                    if (!isVisible) {
                        blackPanel.gameObject.SetActive(false);
                    }

                    completionSource.TrySetResult(true);
                });

            return completionSource.Task;
        }

        void ResetBlackStateImmediate() {
            LeanTween.cancel(blackPanel.gameObject);
            canvasTransitionToken += 1;
            canvasTransitionCompletionSource?.TrySetResult(true);
            canvasTransitionCompletionSource = null;

            blackPanel.alpha = 0f;
            blackPanel.interactable = false;
            blackPanel.blocksRaycasts = false;
            blackPanel.gameObject.SetActive(false);

            ResetSuccessStateImmediate();
        }

        void OnDisable() {
            ResetBlackStateImmediate();
        }

        void PlayTransitionSound(TutorialUiKey? previousKey, TutorialUiKey nextKey) {
            if (!previousKey.HasValue || previousKey.Value == nextKey) {
                return;
            }

            if (IsPracticeKey(nextKey)) {
                practiceStartSound.PlayAtApp(Vector3.zero);
                return;
            }

            if (IsDescriptionKey(nextKey)) {
                descriptionChangeSound.PlayAtApp(Vector3.zero);
            }
        }

        static bool IsDescriptionKey(TutorialUiKey key) {
            return key is TutorialUiKey.WalkDescription
                or TutorialUiKey.DashDescription
                or TutorialUiKey.AttackDescription
                or TutorialUiKey.ChargeDescription
                or TutorialUiKey.ChargeAttackDescription
                or TutorialUiKey.GuardDescription
                or TutorialUiKey.HpDescription
                or TutorialUiKey.SpDescription
                or TutorialUiKey.SpecialDescription
                or TutorialUiKey.Overview;
        }

        static bool IsPracticeKey(TutorialUiKey key) {
            return key is TutorialUiKey.WalkPractice
                or TutorialUiKey.DashPractice
                or TutorialUiKey.AttackPractice
                or TutorialUiKey.ChargePractice
                or TutorialUiKey.ChargeAttackPractice
                or TutorialUiKey.GuardPractice
                or TutorialUiKey.SpecialPractice;
        }

        static Task DelayAsync(float seconds) {
            if (seconds <= 0f) {
                return Task.CompletedTask;
            }

            var completionSource = new TaskCompletionSource<bool>();
            LeanTween.delayedCall(seconds, () => completionSource.TrySetResult(true));
            return completionSource.Task;
        }
    }
}
