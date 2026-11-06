using System;
using System.Threading;
using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Alice {
    public class PlayerFeedbackPresenter : IInitializable, IDisposable {
        readonly ChoiceDialogView view;
        readonly IGamePadRegistry pads;
        readonly IInputFocusService focus;
        readonly ISceneTransitionService transitions;
        readonly PlayerSession session;
        readonly IPlayerFeedbackApiClient api;
        readonly CompositeDisposable subscriptions = new();
        CancellationTokenSource presentation;

        public PlayerFeedbackPresenter(ChoiceDialogView view, IGamePadRegistry pads, IInputFocusService focus,
            ISceneTransitionService transitions, PlayerSession session, IPlayerFeedbackApiClient api) {
            this.view = view;
            this.pads = pads;
            this.focus = focus;
            this.transitions = transitions;
            this.session = session;
            this.api = api;
        }

        public void Initialize() {
            transitions.EndTransitionCompleted.Subscribe(scene => {
                var candidates = session.TakeFeedback(scene);
                if (candidates.Length > 0) _ = PresentAsync(candidates);
            }).AddTo(subscriptions);
            transitions.TransitioningChanged.Where(active => active)
                .Subscribe(_ => presentation?.Cancel()).AddTo(subscriptions);
        }

        async Task PresentAsync(PlayerFeedbackCandidate[] candidates) {
            using var cancellation = new CancellationTokenSource();
            presentation = cancellation;
            var token = cancellation.Token;
            try {
                using var screenHold = focus.HoldScreen();
                using var dialog = new ChoiceDialogPresenter(view, pads, focus, modalPriority: 2);
                using var closeOnCancellation = token.Register(dialog.Dispose);
                foreach (var candidate in candidates) {
                    if (await api.HasFeedbackAsync(candidate.Name, token)) continue;
                    token.ThrowIfCancellationRequested();
                    await dialog.AskAsync("感想のお願い",
                        $"{candidate.Name} さん、プレイありがとうございます\nよろしければ、感想をお聞かせください\nお名前と感想は共有され、送信後は変更できません\n空欄でも送信できます",
                        new[] { "送信" }, "", inputPlayerId: candidate.Slot, directInput: true);
                    await SubmitAsync(candidate, dialog, token);
                }
            } catch (OperationCanceledException) {
            } catch (Exception exception) {
                Debug.LogWarning($"[PlayerFeedbackPresenter] {exception.Message}");
            } finally {
                if (presentation == cancellation) presentation = null;
            }
        }

        async Task SubmitAsync(PlayerFeedbackCandidate candidate, ChoiceDialogPresenter dialog, CancellationToken token) {
            var comment = dialog.InputText;
            while (true) {
                try {
                    await api.SubmitFeedbackAsync(candidate.Name, comment, token);
                    return;
                } catch (ApiRequestException exception) when (exception.StatusCode == 409) {
                    return;
                } catch (ApiRequestException exception) {
                    Debug.LogWarning($"[PlayerFeedbackPresenter] {exception.Message}");
                }
                token.ThrowIfCancellationRequested();
                var choice = await dialog.AskAsync("感想を送信できませんでした", "通信環境を確認して再試行できます",
                    new[] { "再試行", "閉じる" }, inputPlayerId: candidate.Slot, directInput: true);
                if (choice != 0) return;
            }
        }

        public void Dispose() {
            subscriptions.Dispose();
            presentation?.Cancel();
        }
    }
}
