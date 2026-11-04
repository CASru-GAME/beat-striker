using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Alice {
    [System.Serializable]
    public record TutorialDescriptionPages {
        public TutorialUiKey Key;
        public TMP_Text Text;
        public TutorialLessonPage[] Pages;
    }

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
        RhythmDescription,
        RhythmPractice,
        TimerDescription,
        ScoreDescription,
        DefeatPractice,
        ScoreAwardDescription,
        DirectionDescription,
        VictoryDescription,
        CharacterDescription,
        CharacterPractice,
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

        [Header("Narration")]
        [SerializeField] TutorialNarrationView narration;
        [SerializeField] TutorialInputGuideView inputGuide;
        [SerializeField] TMP_Text[] continueHints;
        RectTransform pointedTarget;
        [SerializeField] TutorialDescriptionPages[] descriptionPages;
        TutorialUiKey descriptionNarrationKey;
        string descriptionNarration;

        [Header("Hp")]
        [SerializeField] GameObject hpDescriptionPanel;
        [SerializeField] GameObject hpBarHighlightObject;

        [Header("Sp")]
        [SerializeField] GameObject spDescriptionPanel;
        [SerializeField] GameObject spBarHighlightObject;

        [Header("Bar Pointer")]
        [SerializeField] Image barPointer;
        [SerializeField, Min(0f)] float barPointerTravelSeconds = 0.6f;
        [SerializeField] RectTransform hpBarPointerTarget;
        [SerializeField] RectTransform spBarPointerTarget;
        [SerializeField] Vector2 hpBarPointerTargetPoint = new(0.5f, 0.2f);
        [SerializeField] Vector2 spBarPointerTargetPoint = new(0.5f, 0.1f);
        [SerializeField] Vector2 barPointerTip = new(0.41f, 0.94f);

        [Header("Rhythm")]
        [SerializeField] GameObject rhythmDescriptionPanel;
        [SerializeField] GameObject rhythmPracticePanel;
        [SerializeField] TMP_Text rhythmPracticeText;
        [SerializeField, TextArea] string rhythmPracticeFormat = "{2}のどれかを、ビートに合わせて3回連続成功させよう";
        [SerializeField, Min(0f)] float rhythmPracticeSuccessDisplaySeconds = 0.5f;

        [Header("Walk")]
        [SerializeField] GameObject walkDescriptionPanel;
        [SerializeField] GameObject walkPracticePanel;
        [SerializeField] TMP_Text walkPracticeText;
        [SerializeField] float walkPracticeSuccessDisplaySeconds = 0.8f;
        [SerializeField] float walkVelocityThreshold = 0.2f;
        [SerializeField, Min(0f)] float walkPracticeRequiredTravelDistance = 1.5f;

        [Header("Direction Input")]
        [SerializeField] GameObject directionDescriptionPanel;

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

        [Header("Timer and Score")]
        [SerializeField] GameObject timerDescriptionPanel;
        [SerializeField] GameObject victoryDescriptionPanel;
        [SerializeField] GameObject scoreDescriptionPanel;
        [SerializeField] GameObject defeatPracticePanel;
        [SerializeField] GameObject scoreAwardDescriptionPanel;
        [SerializeField] RectTransform timerPointerTarget;
        [SerializeField] BattlePresenterView battleView;
        [SerializeField] Vector2 timerPointerTargetPoint = new(0.5f, 0f);
        [SerializeField] Vector2 scoreAwardPointerTargetPoint = new(0.5f, 1f);
        [SerializeField] float scorePointerRotationDegrees = 180f;
        [SerializeField, Min(0f)] float defeatPracticeSuccessDisplaySeconds = 0.5f;

        [Header("Final")]
        [SerializeField] GameObject finalPanel;
        [SerializeField] float finalPanelDisplaySeconds = 0.8f;

        [Header("Character Lessons")]
        [SerializeField] GameObject characterDescriptionPanel;
        [SerializeField] TMP_Text characterDescriptionText;
        [SerializeField] TMP_Text characterDescriptionTitle;
        [SerializeField] GameObject characterPracticePanel;
        [SerializeField] TMP_Text characterPracticeText;
        [SerializeField, Min(0f)] float characterPracticeSuccessDisplaySeconds = 0.8f;
        string[] characterLessonNarration;
        string characterLessonTitle;
        TutorialInputCue[] characterCues;
        TutorialControl confirmControl;
        bool characterRetry;
        bool inputGuideSuspended;
        bool attentionActive;

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
        int presentationVersion;
        string rhythmButtons;
        int rhythmSuccessCount;
        int rhythmRequiredCount;

        public float WalkVelocityThreshold => walkVelocityThreshold;
        public float WalkPracticeRequiredTravelDistance => walkPracticeRequiredTravelDistance;

        void Awake() {
            EnsureInitialized();
            EnsureSuccessRuntimeInitialized();
            HideAllPanels();
            ResetBlackStateImmediate();
        }

        public Task ShowAsync(TutorialUiKey key) {
            presentationVersion++;
            EnsureInitialized();
            EnsureSuccessRuntimeInitialized();
            var previousShownKey = hasCurrentShownKey ? currentShownKey : (TutorialUiKey?)null;
            HideAllPanels(true);
            UpdateHighlightObjects(key);
            ShowBarPointer(key);
            if (panelMap.TryGetValue(key, out var panel)) {
                panel.SetActive(true);
            }

            PlayTransitionSound(previousShownKey, key);
            currentShownKey = key;
            hasCurrentShownKey = true;
            inputGuideSuspended = false;
            RefreshInputGuide();
            if (IsPracticeKey(key)) narration.Stop();
            else if (key == TutorialUiKey.CharacterDescription) narration.Play(characterLessonNarration);
            else if (descriptionNarration != null && descriptionNarrationKey == key) narration.Play(new[] { descriptionNarration });
            else narration.Play(key);
            return SetBlackVisibleAsync(IsDescriptionKey(key));
        }

        public async Task HideAfterClearDelayAsync(TutorialUiKey practiceKey) {
            var version = presentationVersion;
            narration.Stop();
            inputGuideSuspended = true;
            inputGuide.Hide();
            await PlaySuccessSequenceAsync();
            if (version != presentationVersion || !isActiveAndEnabled) return;

            var waitSeconds = GetPracticeSuccessWaitSeconds(practiceKey);
            if (waitSeconds > 0f) {
                await DelayAsync(waitSeconds);
            }

            if (version != presentationVersion || !isActiveAndEnabled) return;
            HideAllPanels();
            await SetBlackVisibleAsync(false);
        }

        public async Task HideAfterFinalDelayAsync() {
            var version = presentationVersion;
            await narration.Completion;
            if (version != presentationVersion || !isActiveAndEnabled) return;
            if (finalPanelDisplaySeconds > 0f) {
                await DelayAsync(finalPanelDisplaySeconds);
            }

            if (version != presentationVersion || !isActiveAndEnabled) return;
            HideAllPanels();
            await SetBlackVisibleAsync(false);
        }

        public void HideAll() {
            presentationVersion++;
            narration.Stop();
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
            RegisterPanel(TutorialUiKey.RhythmDescription, rhythmDescriptionPanel);
            RegisterPanel(TutorialUiKey.RhythmPractice, rhythmPracticePanel);

            RegisterPanel(TutorialUiKey.DirectionDescription, directionDescriptionPanel);
            RegisterPanel(TutorialUiKey.VictoryDescription, victoryDescriptionPanel);
            RegisterPanel(TutorialUiKey.TimerDescription, timerDescriptionPanel);
            RegisterPanel(TutorialUiKey.ScoreDescription, scoreDescriptionPanel);
            RegisterPanel(TutorialUiKey.DefeatPractice, defeatPracticePanel);
            RegisterPanel(TutorialUiKey.ScoreAwardDescription, scoreAwardDescriptionPanel);
            RegisterPanel(TutorialUiKey.CharacterDescription, characterDescriptionPanel);
            RegisterPanel(TutorialUiKey.CharacterPractice, characterPracticePanel);

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

        void HideAllPanels(bool preservePointer = false) {
            inputGuideSuspended = true;
            attentionActive = false;
            inputGuide.Hide();
            foreach (var panel in panelMap.Values) {
                panel.SetActive(false);
            }

            SetHighlightObjectsActive(false);
            if (!preservePointer) HideBarPointer();
            ResetSuccessStateImmediate();
        }

        void UpdateHighlightObjects(TutorialUiKey key) {
            hpBarHighlightObject.SetActive(key == TutorialUiKey.HpDescription);
            spBarHighlightObject.SetActive(key == TutorialUiKey.SpDescription || key == TutorialUiKey.SpecialDescription);
        }

        void ShowBarPointer(TutorialUiKey key) {
            var target = key switch {
                TutorialUiKey.TimerDescription => timerPointerTarget,
                TutorialUiKey.ScoreAwardDescription => battleView.LatestRoundWinTrophy,
                TutorialUiKey.HpDescription => hpBarPointerTarget,
                TutorialUiKey.SpDescription or TutorialUiKey.SpecialDescription => spBarPointerTarget,
                _ => null,
            };
            if (target == null) { HideBarPointer(); return; }
            if (target == pointedTarget && barPointer.gameObject.activeSelf) return;
            LeanTween.cancel(barPointer.gameObject);
            pointedTarget = target;

            var pointerRect = barPointer.rectTransform;
            var parentRect = (RectTransform)pointerRect.parent;
            barPointer.gameObject.SetActive(true);
            pointerRect.localRotation = Quaternion.Euler(0f, 0f,
                key == TutorialUiKey.ScoreAwardDescription ? scorePointerRotationDegrees : 0f);
            pointerRect.anchoredPosition = Vector2.zero;
            pointerRect.localScale = Vector3.one * 0.7f;
            barPointer.color = new Color(1f, 1f, 1f, 0f);

            void UpdatePointer(float progress) {
                pointerRect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, EaseOutCubic(progress));
                var tipLocal = pointerRect.rect.min + Vector2.Scale(pointerRect.rect.size, barPointerTip);
                var tipOffset = (Vector2)parentRect.InverseTransformVector(pointerRect.TransformVector(tipLocal));
                var targetPoint = key switch {
                    TutorialUiKey.HpDescription => hpBarPointerTargetPoint,
                    TutorialUiKey.SpDescription or TutorialUiKey.SpecialDescription => spBarPointerTargetPoint,
                    TutorialUiKey.TimerDescription => timerPointerTargetPoint,
                    TutorialUiKey.ScoreAwardDescription => scoreAwardPointerTargetPoint,
                    _ => new Vector2(0.5f, 0.5f),
                };
                var targetLocal = target.rect.min + Vector2.Scale(target.rect.size, targetPoint);
                // Fill scales change with HP/SP. Aim at the full gauge, including when SP is empty.
                var isGauge = key is TutorialUiKey.HpDescription or TutorialUiKey.SpDescription or TutorialUiKey.SpecialDescription;
                var targetWorld = isGauge
                    ? target.parent.TransformPoint(target.localPosition + target.localRotation * (Vector3)targetLocal)
                    : target.TransformPoint(targetLocal);
                var targetPosition = (Vector2)parentRect.InverseTransformPoint(targetWorld) - tipOffset;
                pointerRect.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, targetPosition, EaseOutCubic(progress));
                barPointer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(progress * 5f));
            }

            if (barPointerTravelSeconds <= 0f) {
                UpdatePointer(1f);
                return;
            }

            LeanTween.value(barPointer.gameObject, 0f, 1f, barPointerTravelSeconds)
                .setIgnoreTimeScale(true)
                .setOnUpdate((float progress) => UpdatePointer(progress));
        }

        public void SetRhythmProgress(int successCount, int requiredCount) {
            rhythmSuccessCount = successCount;
            rhythmRequiredCount = requiredCount;
            rhythmPracticeText.text = string.Format(rhythmPracticeFormat, successCount, requiredCount, rhythmButtons, Mathf.Max(0, requiredCount - successCount));
            if (hasCurrentShownKey && currentShownKey == TutorialUiKey.RhythmPractice) {
                RefreshInputGuide();
                inputGuide.PulseOnBeat();
            }
        }

        public void SetNarrationInput(TutorialVoiceInput input) {
            narration.SetInput(input);
            var confirmLabel = input.East.StartsWith("key_") ? input.East.Substring(4).ToUpperInvariant() + "キー" : input.East.Substring(7).ToUpperInvariant() + "ボタン";
            foreach (var hint in continueHints) hint.text = confirmLabel + "で次へ";
            walkPracticeText.text = input.Walk == "walk_keyboard" ? "AキーかDキー長押しして歩いてみよう" : "スティックを左か右に倒したまま歩いてみよう";
            rhythmButtons = input.RhythmButtons;
            SetRhythmProgress(rhythmSuccessCount, rhythmRequiredCount);
        }

        public void SetNarrationVolume(float volume) => narration.SetVolume(volume);

        public int GetDescriptionPageCount(TutorialUiKey key) => System.Array.Find(descriptionPages, item => item.Key == key).Pages.Length;

        public void SetDescriptionPage(TutorialUiKey key, int page) {
            var group = System.Array.Find(descriptionPages, item => item.Key == key);
            group.Text.text = group.Pages[page].Text;
            descriptionNarrationKey = key;
            descriptionNarration = group.Pages[page].Voice;
        }

        public void SetCharacterLessonText(string title, string description) {
            characterDescriptionTitle.text = title;
            characterLessonTitle = title;
            characterDescriptionText.text = description;
        }

        public void SetCharacterPracticeStep(string instruction, int count, int required, bool retry, TutorialInputCue[] cues) {
            characterPracticeText.text = instruction;
            characterCues = cues;
            characterRetry = retry;
            if (hasCurrentShownKey && currentShownKey == TutorialUiKey.CharacterPractice) RefreshInputGuide();
        }

        public void SetInputLayout(bool keyboard, TutorialControl confirm) {
            inputGuide.SetLayout(keyboard);
            confirmControl = confirm;
            if (hasCurrentShownKey) RefreshInputGuide();
        }

        public void SetGuideBeatSeconds(float seconds) => inputGuide.SetBeatSeconds(seconds);

        public void SetAttentionActive(bool active) {
            if (attentionActive == active) return;
            attentionActive = active;
            if (hasCurrentShownKey) RefreshInputGuide();
        }

        public void PulseRhythmGuide() {
            if (hasCurrentShownKey && currentShownKey == TutorialUiKey.RhythmPractice) inputGuide.PulseOnBeat();
        }

        void RefreshInputGuide() {
            if (inputGuideSuspended || attentionActive || !panelMap.TryGetValue(currentShownKey, out var panel) || !panel.activeSelf) {
                inputGuide.Hide();
                return;
            }
            if (currentShownKey == TutorialUiKey.CharacterPractice) {
                if (characterCues != null) inputGuide.ShowSequence(characterCues);
                else inputGuide.Hide();
            }
            else if (currentShownKey == TutorialUiKey.WalkPractice)
                inputGuide.Show(new TutorialInputCue(TutorialControl.None, Vector2.right, AlternateDirection: true));
            else if (currentShownKey == TutorialUiKey.RhythmPractice)
                inputGuide.ShowSequence(new[] {
                    new TutorialInputCue(TutorialControl.North, Vector2.zero),
                    new TutorialInputCue(TutorialControl.West, Vector2.zero),
                    new TutorialInputCue(TutorialControl.South, Vector2.zero),
                    new TutorialInputCue(TutorialControl.East, Vector2.zero),
                });
            else inputGuide.Hide();
        }

        public void SetCharacterLessonNarration(string[] segments) => characterLessonNarration = segments;

        void HideBarPointer() {
            pointedTarget = null;
            LeanTween.cancel(barPointer.gameObject);
            barPointer.gameObject.SetActive(false);
        }

        void SetHighlightObjectsActive(bool isActive) {
            hpBarHighlightObject.SetActive(isActive);
            spBarHighlightObject.SetActive(isActive);
        }

        float GetPracticeSuccessWaitSeconds(TutorialUiKey practiceKey) {
            return practiceKey switch {
                TutorialUiKey.RhythmPractice => rhythmPracticeSuccessDisplaySeconds,
                TutorialUiKey.WalkPractice => walkPracticeSuccessDisplaySeconds,
                TutorialUiKey.DashPractice => dashPracticeSuccessDisplaySeconds,
                TutorialUiKey.AttackPractice => attackPracticeSuccessDisplaySeconds,
                TutorialUiKey.ChargePractice => chargePracticeSuccessDisplaySeconds,
                TutorialUiKey.ChargeAttackPractice => chargeAttackPracticeSuccessDisplaySeconds,
                TutorialUiKey.GuardPractice => guardPracticeSuccessDisplaySeconds,
                TutorialUiKey.SpecialPractice => specialPracticeSuccessDisplaySeconds,
                TutorialUiKey.DefeatPractice => defeatPracticeSuccessDisplaySeconds,
                TutorialUiKey.CharacterPractice => characterPracticeSuccessDisplaySeconds,
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
            presentationVersion++;
            HideBarPointer();
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
            return key is TutorialUiKey.Final or TutorialUiKey.WalkDescription
                or TutorialUiKey.CharacterDescription
                or TutorialUiKey.DashDescription
                or TutorialUiKey.AttackDescription
                or TutorialUiKey.ChargeDescription
                or TutorialUiKey.ChargeAttackDescription
                or TutorialUiKey.GuardDescription
                or TutorialUiKey.HpDescription
                or TutorialUiKey.SpDescription
                or TutorialUiKey.SpecialDescription
                or TutorialUiKey.RhythmDescription
                or TutorialUiKey.TimerDescription
                or TutorialUiKey.VictoryDescription
                or TutorialUiKey.DirectionDescription
                or TutorialUiKey.ScoreDescription
                or TutorialUiKey.ScoreAwardDescription
                or TutorialUiKey.Overview;
        }

        static bool IsPracticeKey(TutorialUiKey key) {
            return key is TutorialUiKey.RhythmPractice
                or TutorialUiKey.CharacterPractice
                or TutorialUiKey.WalkPractice
                or TutorialUiKey.DashPractice
                or TutorialUiKey.AttackPractice
                or TutorialUiKey.ChargePractice
                or TutorialUiKey.ChargeAttackPractice
                or TutorialUiKey.GuardPractice
                or TutorialUiKey.SpecialPractice
                or TutorialUiKey.DefeatPractice;
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
