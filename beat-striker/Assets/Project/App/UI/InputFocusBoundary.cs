using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VContainer;

namespace Alice {
    public sealed class InputFocusBoundary : MonoBehaviour {
        [SerializeField] EventSystem eventSystem;
        [SerializeField] BaseInputModule inputModule;
        [SerializeField] ScrollRect[] scrollRects;
        [SerializeField] Selectable[] selectables;
        readonly System.Collections.Generic.HashSet<Selectable> disabledSelectables = new();
        IInputFocusService focus;
        bool resetPending;
        readonly System.Collections.Generic.HashSet<ScrollRect> disabledScrolls = new();
        int resetFrame;
        [Inject] public void Initialize(IInputFocusService focus) {
            this.focus = focus;
            if (inputModule is InputSystemUIInputModule uiInput) {
                // GamePadRegistry routes the pad's face buttons through the player cursor and dialogs.
                uiInput.submit.action.ApplyBindingOverride("<Keyboard>/{Submit}", path: "*/{Submit}");
                uiInput.cancel.action.ApplyBindingOverride("<Keyboard>/{Cancel}", path: "*/{Cancel}");
            }
            focus.Changed.Subscribe(_ => ResetInput()).AddTo(this);
            ResetInput();
        }
        void ResetInput() {
            resetFrame = Time.frameCount;
            resetPending = true;
            if (!eventSystem.alreadySelecting) eventSystem.SetSelectedGameObject(null);
            foreach (var selectable in selectables) {
                if (!selectable) continue;
                if (focus.CanPoint(selectable.transform)) { if (disabledSelectables.Remove(selectable)) selectable.enabled = true; }
                else if (selectable.enabled) { disabledSelectables.Add(selectable); selectable.enabled = false; }
            }
            foreach (var scroll in scrollRects) {
                if (!scroll) continue;
                if (focus.CanPoint(scroll.transform)) { if (disabledScrolls.Remove(scroll)) scroll.enabled = true; continue; }
                scroll.StopMovement();
                scroll.OnEndDrag(new PointerEventData(eventSystem));
                if (scroll.enabled) { disabledScrolls.Add(scroll); scroll.enabled = false; }
            }
        }
        void Update() {
            var selected = eventSystem.currentSelectedGameObject;
            if (focus != null && selected && !focus.CanPoint(selected.transform) && !eventSystem.alreadySelecting) eventSystem.SetSelectedGameObject(null);
        }
        void LateUpdate() {
            if (!resetPending) return;
            // Reset outside Process: a modal may have opened inside a pointer callback.
            inputModule.enabled = false;
            if (Time.frameCount <= resetFrame || Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed)
                ) return;
            if (Touchscreen.current != null) foreach (var touch in Touchscreen.current.touches) if (touch.press.isPressed) return;
            inputModule.enabled = true;
            resetPending = false;
        }
    }
}
