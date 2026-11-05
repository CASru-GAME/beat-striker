using System;
using System.Threading.Tasks;
using R3;
using UnityEngine.SceneManagement;

namespace Alice {
    public class ChoiceDialogPresenter : IDisposable {
        readonly ChoiceDialogView view;
        readonly IGamePadRegistry pads;
        readonly IInputFocusService focus;
        readonly int sceneHandle;
        IDisposable modal;
        readonly CompositeDisposable subscriptions = new();
        TaskCompletionSource<int> completion;
        bool directInput, disposed, closing;
        int optionCount, inputPlayerId, openedFrame;
        public string InputText { get; private set; }
        public ChoiceDialogPresenter(ChoiceDialogView view, IGamePadRegistry pads, IInputFocusService focus) {
            this.view = view; this.pads = pads; this.focus = focus;
            sceneHandle = view.gameObject.scene.handle;
            view.InputRaycaster.Initialize(focus);
            focus.Changed.Subscribe(_ => {
                if (!ViewAvailable()) return;
                view.SetInputAllowed(focus.CanPoint(view.ModalRoot));
            }).AddTo(subscriptions);
            view.Selected.Subscribe(Resolve).AddTo(subscriptions);
            view.Disabled.Subscribe(_ => { if (!closing) CancelPending(); }).AddTo(subscriptions);
            view.Destroyed.Subscribe(_ => Dispose()).AddTo(subscriptions);
            view.InputFocusChanged.Subscribe(active => { focus.SetTextFocus(view.ModalRoot, active); }).AddTo(subscriptions);
            pads.OnAnyButtonDown.Subscribe(input => {
                if (!ViewAvailable()) return;
                if (!focus.CanReceive(view.ModalRoot, input.PlayerId)) return;
                if (completion == null || !directInput || inputPlayerId >= 0 && input.PlayerId != inputPlayerId) return;
                UpdateButtonGuides(input.PlayerId);
                if (input.Button == GamePadButton.East) Resolve(0);
                else if (input.Button == GamePadButton.South && optionCount > 1) Resolve(optionCount - 1);
            }).AddTo(subscriptions);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        bool ViewAvailable() {
            if (view) return true;
            Dispose();
            return false;
        }
        void OnSceneUnloaded(Scene scene) {
            if (scene.handle == sceneHandle) Dispose();
        }
        void Resolve(int choice) {
            if (!focus.CanReceive(view.ModalRoot)) return;
            if (completion == null || closing || !view.IsInteractable || UnityEngine.Time.frameCount <= openedFrame) return;
            closing = true;
            var pending = completion;
            InputText = view.InputText;
            view.Close(() => {
                completion = null;
                modal?.Dispose(); modal = null;
                closing = false;
                pending.TrySetResult(choice);
            });
        }
        public Task<int> AskAsync(string title, string body, string[] options, string input = null, int inputPlayerId = -1, bool directInput = false) {
            if (disposed || !ViewAvailable()) throw new ObjectDisposedException(nameof(ChoiceDialogPresenter));
            if (completion != null) throw new InvalidOperationException("The dialog is already open.");
            if (options == null || options.Length == 0 || options.Length > view.OptionCapacity) throw new ArgumentException("The options exceed the dialog's authored button capacity.", nameof(options));
            // Finish the current input dispatch before the caller changes scene input state.
            // Otherwise a dialog click can also reopen the dialog in a later subscriber.
            var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            completion = pending;
            closing = false;
            this.directInput = directInput; this.inputPlayerId = inputPlayerId; optionCount = options.Length; openedFrame = UnityEngine.Time.frameCount;
            try {
                modal = focus.RegisterModal(view.ModalRoot, inputPlayerId);
                view.Show(title, body, options, input, inputPlayerId);
                _ = focus.Version;
                view.SetInputAllowed(focus.CanPoint(view.ModalRoot));
                UpdateButtonGuides(inputPlayerId >= 0 ? inputPlayerId : 0);
                return pending.Task;
            } catch {
                completion = null;
                modal?.Dispose(); modal = null;
                view.Hide();
                throw;
            }
        }
        void UpdateButtonGuides(int playerId) {
            var keyboard = pads.Get(playerId).DeviceName == "Keybord";
            var confirm = pads.GetPhysicalFaceButton(GamePadButton.East);
            var cancel = pads.GetPhysicalFaceButton(GamePadButton.South);
            view.SetButtonGuides(directInput ? Guide(confirm, keyboard) : null, directInput ? Guide(cancel, keyboard) : null, keyboard);
        }
        static string Guide(GamePadButton button, bool keyboard) {
            if (!keyboard) return button.ToString();
            return button switch {
                GamePadButton.North => "I",
                GamePadButton.West => "J",
                GamePadButton.South => "K",
                GamePadButton.East => "L",
                _ => throw new ArgumentOutOfRangeException(nameof(button)),
            };
        }
        public void Dispose() {
            if (disposed) return;
            disposed = true;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            subscriptions.Dispose(); CancelPending();
        }
        void CancelPending() {
            var pending = completion; completion = null;
            modal?.Dispose(); modal = null;
            if (pending != null && view != null) view.Hide();
            pending?.TrySetCanceled();
        }
    }
}
