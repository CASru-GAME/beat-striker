using System;
using System.Threading.Tasks;
using R3;

namespace Alice {
    public class ChoiceDialogPresenter : IDisposable {
        readonly ChoiceDialogView view;
        readonly PlayerSession session;
        readonly IGamePadRegistry pads;
        readonly CompositeDisposable subscriptions = new();
        TaskCompletionSource<int> completion;
        bool directInput, disposed, closing;
        int optionCount, inputPlayerId, openedFrame;
        public string InputText { get; private set; }
        public ChoiceDialogPresenter(ChoiceDialogView view, PlayerSession session, IGamePadRegistry pads) {
            this.view = view; this.session = session; this.pads = pads;
            view.Selected.Subscribe(Resolve).AddTo(subscriptions);
            view.Destroyed.Subscribe(_ => Dispose()).AddTo(subscriptions);
            view.InputFocusChanged.Subscribe(session.SetNameInputActive).AddTo(subscriptions);
            pads.OnAnyButtonDown.Subscribe(input => {
                if (completion == null || !directInput || inputPlayerId >= 0 && input.PlayerId != inputPlayerId) return;
                UpdateButtonGuides(input.PlayerId);
                if (input.Button == GamePadButton.East) _ = ConfirmDefaultAfterPointerAsync();
                else if (input.Button == GamePadButton.South && optionCount > 1) Resolve(optionCount - 1);
            }).AddTo(subscriptions);
        }
        async Task ConfirmDefaultAfterPointerAsync() {
            if (closing || !view.IsInteractable || UnityEngine.Time.frameCount <= openedFrame) return;
            var pending = completion;
            // The same A input also clicks the cursor target. Give that explicit click
            // priority, and use the default action only if no option handled it.
            await UnityEngine.Awaitable.NextFrameAsync();
            if (pending == completion && !closing && !disposed) Resolve(0);
        }
        void Resolve(int choice) {
            if (completion == null || closing || !view.IsInteractable || UnityEngine.Time.frameCount <= openedFrame) return;
            closing = true;
            var pending = completion;
            InputText = view.InputText;
            view.Close(() => {
                completion = null;
                closing = false;
                session.SetNameInputActive(false);
                pending.TrySetResult(choice);
            });
        }
        public Task<int> AskAsync(string title, string body, string[] options, string input = null, int inputPlayerId = -1, bool directInput = false) {
            if (disposed) throw new ObjectDisposedException(nameof(ChoiceDialogPresenter));
            if (completion != null) throw new InvalidOperationException("The dialog is already open.");
            if (options == null || options.Length == 0 || options.Length > view.OptionCapacity) throw new ArgumentException("The options exceed the dialog's authored button capacity.", nameof(options));
            // Finish the current input dispatch before the caller changes scene input state.
            // Otherwise a dialog click can also reopen the dialog in a later subscriber.
            var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            completion = pending;
            closing = false;
            this.directInput = directInput; this.inputPlayerId = inputPlayerId; optionCount = options.Length; openedFrame = UnityEngine.Time.frameCount;
            try {
                view.Show(title, body, options, input, inputPlayerId);
                UpdateButtonGuides(inputPlayerId >= 0 ? inputPlayerId : 0);
                return pending.Task;
            } catch {
                completion = null;
                session.SetNameInputActive(false);
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
            subscriptions.Dispose(); CancelPending();
        }
        void CancelPending() {
            if (completion != null && view != null) view.Hide();
            session.SetNameInputActive(false);
            completion?.TrySetCanceled(); completion = null;
        }
    }
}
