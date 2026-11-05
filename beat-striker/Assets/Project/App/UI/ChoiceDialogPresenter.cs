using System;
using System.Threading.Tasks;
using R3;

namespace Alice {
    public class ChoiceDialogPresenter : IDisposable {
        readonly ChoiceDialogView view;
        readonly PlayerSession session;
        readonly CompositeDisposable subscriptions = new();
        TaskCompletionSource<int> completion;
        bool directInput, disposed;
        int selectedIndex, optionCount, inputPlayerId, openedFrame;
        readonly int[] directions = new int[4];
        public string InputText => view.InputText;
        public ChoiceDialogPresenter(ChoiceDialogView view, PlayerSession session, IGamePadRegistry pads) {
            this.view = view; this.session = session;
            view.Selected.Subscribe(Resolve).AddTo(subscriptions);
            view.Destroyed.Subscribe(_ => Dispose()).AddTo(subscriptions);
            pads.OnAnyButtonDown.Subscribe(input => {
                if (completion == null || !directInput || inputPlayerId >= 0 && input.PlayerId != inputPlayerId) return;
                if (input.Button == GamePadButton.East) Resolve(selectedIndex);
                else if (input.Button == GamePadButton.South) Resolve(optionCount - 1);
                else if (input.Button == GamePadButton.Left) Move(-1);
                else if (input.Button == GamePadButton.Right) Move(1);
            }).AddTo(subscriptions);
            for (var slot = 0; slot < 4; slot++) {
                var playerId = slot;
                pads.Get(slot).OnDirection.Subscribe(direction => {
                    var axis = direction.y > .5f ? -1 : direction.y < -.5f ? 1 : 0;
                    if (axis != 0 && axis != directions[playerId] && completion != null && directInput && (inputPlayerId < 0 || inputPlayerId == playerId)) Move(axis);
                    directions[playerId] = axis;
                }).AddTo(subscriptions);
                pads.Get(slot).OnDirectionCanceled.Subscribe(_ => directions[playerId] = 0).AddTo(subscriptions);
            }
        }
        void Move(int direction) { selectedIndex = (selectedIndex + direction + optionCount) % optionCount; view.SetSelection(selectedIndex); }
        void Resolve(int choice) {
            if (completion == null || UnityEngine.Time.frameCount <= openedFrame) return;
            var pending = completion; completion = null;
            view.Hide(); session.SetNameInputActive(false); pending.TrySetResult(choice);
        }
        public Task<int> AskAsync(string title, string body, string[] options, string input = null, int inputPlayerId = -1, bool directInput = false) {
            if (disposed) throw new ObjectDisposedException(nameof(ChoiceDialogPresenter));
            if (completion != null) throw new InvalidOperationException("The dialog is already open.");
            if (options == null || options.Length == 0 || options.Length > 5) throw new ArgumentException("A dialog requires one to five options.", nameof(options));
            var pending = new TaskCompletionSource<int>();
            completion = pending;
            this.directInput = directInput; this.inputPlayerId = inputPlayerId; optionCount = options.Length; selectedIndex = 0; openedFrame = UnityEngine.Time.frameCount;
            session.SetNameInputActive(input != null);
            try {
                view.Show(title, body, options, input, inputPlayerId);
                if (directInput) view.SetSelection(selectedIndex);
                return pending.Task;
            } catch {
                completion = null;
                session.SetNameInputActive(false);
                throw;
            }
        }
        public void Dispose() {
            if (disposed) return;
            disposed = true;
            subscriptions.Dispose(); CancelPending();
        }
        void CancelPending() {
            session.SetNameInputActive(false);
            completion?.TrySetCanceled(); completion = null;
        }
    }
}
