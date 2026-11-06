using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Alice {
    [RequireComponent(typeof(FocusGraphicRaycaster))]
    public class ChoiceDialogView : MonoBehaviour {
        [SerializeField] GameObject dialogRoot;
        [SerializeField] TMP_Text heading;
        [SerializeField] TMP_Text description;
        [SerializeField] TMP_Text gradedDescription;
        [SerializeField] GradeBadgeView gradeView;
        [SerializeField] GradeBadgeView selectedGradeView;
        [SerializeField] TMP_InputField input;
        [SerializeField] GameObject inputGuide;
        [SerializeField, Min(0)] int inputCodePointLimit;
        [SerializeField] ActionEmitter[] emitters;
        [SerializeField] TMP_Text[] labels;
        [SerializeField] ActionEmitter previousValue;
        [SerializeField] ActionEmitter nextValue;
        [SerializeField] GameObject[] keyboardKeys;
        [SerializeField] TMP_Text[] keyboardKeyLabels;
        [SerializeField] RectTransform panel;
        [SerializeField] AudioClip selectionSound;
        [SerializeField, Min(0.01f)] float openDuration = 0.22f;
        [SerializeField, Min(0.01f)] float closeDuration = 0.16f;
        [SerializeField, Min(0.01f)] float hoverDuration = 0.12f;
        [SerializeField, Range(1f, 1.3f)] float hoverScale = 1.08f;
        readonly Subject<int> selected = new();
        readonly Subject<int> valueStepRequested = new();
        readonly Subject<Unit> destroyed = new();
        readonly Subject<Unit> disabled = new();
        readonly Subject<bool> inputFocusChanged = new();
        int shownFrame = -1;
        int inputPlayerId = -1;
        bool hasInputFocus;
        bool inputAllowed = true;
        string[] options = System.Array.Empty<string>();
        readonly List<RaycastResult> pointerHits = new();
        public Observable<int> Selected => selected;
        public Observable<int> ValueStepRequested => valueStepRequested;
        public Observable<Unit> Destroyed => destroyed;
        public Observable<Unit> Disabled => disabled;
        public Observable<bool> InputFocusChanged => inputFocusChanged;
        public void SetInputAllowed(bool allowed) {
            inputAllowed = allowed;
            input.interactable = allowed && IsInteractable;
            if (!allowed) ReleaseInputFocus();
        }
        public FocusGraphicRaycaster InputRaycaster => GetComponent<FocusGraphicRaycaster>();
        public Transform ModalRoot => dialogRoot.transform;
        public int OptionCapacity => emitters.Length;
        public string InputText => input.text;
        public bool IsInteractable { get; private set; }
        void Awake() {
            for (var i = 0; i < emitters.Length; i++) {
                var index = i;
                emitters[i].OnClickEvent.Subscribe(data => {
                    if (CanClick(data)) selected.OnNext(index);
                }).AddTo(this);
                BindHover(emitters[i]);
            }
            previousValue.OnClickEvent.Subscribe(data => { if (CanClick(data)) valueStepRequested.OnNext(-1); }).AddTo(this);
            nextValue.OnClickEvent.Subscribe(data => { if (CanClick(data)) valueStepRequested.OnNext(1); }).AddTo(this);
            BindHover(previousValue);
            BindHover(nextValue);
            input.onSubmit.AddListener(EndEditing);
            input.onEndEdit.AddListener(EndEditing);
            input.onSelect.AddListener(InputSelected);
            input.onDeselect.AddListener(InputDeselected);
            if (inputCodePointLimit > 0) input.onValueChanged.AddListener(LimitInput);
        }
        bool CanClick(Core.BotanEventData data) => IsInteractable && Time.frameCount > shownFrame
            && (inputPlayerId < 0 || data.EventData is not CursorPointerEventData cursor || cursor.PlayerId == inputPlayerId);

        void Update() {
            if (IsInteractable && previousValue.gameObject.activeSelf && Keyboard.current is { } keyboard) {
                var step = (keyboard.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (keyboard.leftArrowKey.wasPressedThisFrame ? 1 : 0);
                if (step != 0) valueStepRequested.OnNext(step);
            }
            if (!input.isFocused) return;
            var eventSystem = EventSystem.current;
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) {
                ReleaseInputFocus();
                return;
            }
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            var pointer = new PointerEventData(eventSystem) { position = Mouse.current.position.ReadValue() };
            pointerHits.Clear();
            eventSystem.RaycastAll(pointer, pointerHits);
            if (pointerHits.Count > 0 && pointerHits[0].gameObject.transform.IsChildOf(input.transform)) return;
            ReleaseInputFocus();
        }
        public void Show(string title, string body, string[] options, string initialInput = null, int inputPlayerId = -1, int? grade = null) {
            ReleaseInputFocus();
            shownFrame = Time.frameCount; this.inputPlayerId = inputPlayerId;
            this.options = options;
            heading.text = title; SetDescription(body); description.richText = false;
            SetValueArrowsVisible(false);
            selectedGradeView.gameObject.SetActive(false);
            description.gameObject.SetActive(!grade.HasValue);
            gradedDescription.gameObject.SetActive(grade.HasValue);
            gradedDescription.text = body;
            gradeView.gameObject.SetActive(grade.HasValue);
            if (grade.HasValue) gradeView.Set(grade.Value);
            for (var i = 0; i < emitters.Length; i++) {
                emitters[i].gameObject.SetActive(i < options.Length);
                if (i < options.Length) labels[i].text = options[i];
            }
            var hasInput = initialInput != null;
            inputGuide.SetActive(hasInput);
            input.gameObject.SetActive(hasInput);
            if (hasInput) input.SetTextWithoutNotify(initialInput);
            dialogRoot.SetActive(true);
            IsInteractable = false;
            input.interactable = false;
            LeanTween.cancel(panel.gameObject);
            panel.localScale = Vector3.zero;
            LeanTween.scale(panel.gameObject, Vector3.one, openDuration).setIgnoreTimeScale(true).setEaseOutCubic().setOnComplete(() => {
                IsInteractable = true;
                input.interactable = inputAllowed;
            });
        }
        public void SetDescription(string body) => description.text = body;
        public void SetSelectedGrade(PlayerGrade grade) {
            description.gameObject.SetActive(false);
            selectedGradeView.gameObject.SetActive(true);
            selectedGradeView.Set((int)grade);
        }
        public void SetValueArrowsVisible(bool visible) {
            previousValue.gameObject.SetActive(visible);
            nextValue.gameObject.SetActive(visible);
        }
        public void PlaySelectionSound() => selectionSound.PlayAtApp();
        public void Close(Action onClosed) {
            IsInteractable = false;
            ReleaseInputFocus();
            input.interactable = false;
            LeanTween.cancel(panel.gameObject);
            LeanTween.scale(panel.gameObject, Vector3.zero, closeDuration).setIgnoreTimeScale(true).setEaseInCubic().setOnComplete(() => {
                dialogRoot.SetActive(false);
                onClosed();
            });
        }
        public void Hide() { LeanTween.cancel(panel.gameObject); IsInteractable = false; ReleaseInputFocus(); dialogRoot.SetActive(false); }
        void BindHover(ActionEmitter emitter) {
            emitter.OnHoverEvent.Subscribe(_ => AnimateButton(emitter.gameObject, true)).AddTo(this);
            emitter.OnHoverExitEvent.Subscribe(_ => AnimateButton(emitter.gameObject, false)).AddTo(this);
        }
        void AnimateButton(GameObject button, bool hovered) {
            if (!IsInteractable) return;
            LeanTween.cancel(button);
            LeanTween.scale(button, Vector3.one * (hovered ? hoverScale : 1f), hoverDuration).setIgnoreTimeScale(true).setEaseOutCubic();
        }
        void ResetButtonScale(ActionEmitter emitter) {
            LeanTween.cancel(emitter.gameObject);
            emitter.transform.localScale = Vector3.one;
        }
        public void SetButtonGuides(string confirmIcon, string cancelIcon, bool keyboard = false) {
            for (var i = 0; i < options.Length; i++) {
                labels[i].richText = true;
                var icon = i == 0 ? confirmIcon : i == options.Length - 1 ? cancelIcon : null;
                keyboardKeys[i].SetActive(keyboard && icon != null);
                keyboardKeyLabels[i].text = keyboard ? icon ?? "" : "";
                labels[i].text = (icon == null || keyboard ? "" : $"<sprite name=\"{icon}\" tint=1> ") + options[i];
            }
        }
        void EndEditing(string text) => ReleaseInputFocus();
        void LimitInput(string text) {
            var end = 0;
            for (var count = 0; count < inputCodePointLimit && end < text.Length; count++) {
                end += char.IsSurrogatePair(text, end) ? 2 : 1;
            }
            if (end < text.Length) input.SetTextWithoutNotify(text.Substring(0, end));
        }
        void InputSelected(string text) => SetInputFocus(true);
        void InputDeselected(string text) => SetInputFocus(false);
        void ReleaseInputFocus() {
            input.DeactivateInputField(true);
            var eventSystem = EventSystem.current;
            if (eventSystem != null && !eventSystem.alreadySelecting && eventSystem.currentSelectedGameObject == input.gameObject) {
                eventSystem.SetSelectedGameObject(null);
            }
            SetInputFocus(false);
        }
        void SetInputFocus(bool focused) {
            if (hasInputFocus == focused) return;
            hasInputFocus = focused;
            inputFocusChanged.OnNext(focused);
        }
        void OnDisable() {
            disabled.OnNext(Unit.Default);
            IsInteractable = false;
            ReleaseInputFocus();
            LeanTween.cancel(panel.gameObject);
            foreach (var emitter in emitters) ResetButtonScale(emitter);
            ResetButtonScale(previousValue);
            ResetButtonScale(nextValue);
        }
        void OnDestroy() {
            input.onSubmit.RemoveListener(EndEditing);
            input.onEndEdit.RemoveListener(EndEditing);
            input.onSelect.RemoveListener(InputSelected);
            input.onDeselect.RemoveListener(InputDeselected);
            input.onValueChanged.RemoveListener(LimitInput);
            destroyed.OnNext(Unit.Default); destroyed.Dispose(); disabled.Dispose(); selected.Dispose(); inputFocusChanged.Dispose();
            valueStepRequested.Dispose();
        }
    }
}
