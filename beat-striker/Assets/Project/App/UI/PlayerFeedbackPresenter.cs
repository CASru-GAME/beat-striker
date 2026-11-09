using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Alice {
    public class PlayerFeedbackPresenter : IInitializable, IDisposable {
        const int RequiredCompletedBattles = 2;

        readonly ChoiceDialogView view;
        readonly IGamePadRegistry pads;
        readonly IInputFocusService focus;
        readonly ISceneTransitionService transitions;
        readonly PlayerSession session;
        readonly IPlayerFeedbackApiClient api;
        readonly CompositeDisposable subscriptions = new();
        readonly HashSet<string> submittedNames = new(StringComparer.Ordinal);
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
            transitions.TransitioningChanged.Where(active => active)
                .Subscribe(_ => presentation?.Cancel()).AddTo(subscriptions);
        }

        internal string[] RecordCompletedBattle() {
            if (session.Mode is not (BattlePlayMode.Local or BattlePlayMode.Trial)) return Array.Empty<string>();

            var names = session.Participants
                .Where(player => !player.isCpu && !string.IsNullOrWhiteSpace(player.name) && player.name != PlayerProfileService.GuestName)
                .Select(player => player.name)
                .Distinct(StringComparer.Ordinal);
            var candidates = new List<string>();
            var savedCountChanged = false;
            try {
                foreach (var name in names) {
                    if (submittedNames.Contains(name)) continue;
                    var key = $"PlayerFeedback.CompletedBattles.{Hash128.Compute(name)}";
                    var count = PlayerPrefs.GetInt(key);
                    if (count < RequiredCompletedBattles) {
                        count++;
                        PlayerPrefs.SetInt(key, count);
                        savedCountChanged = true;
                    }
                    if (count >= RequiredCompletedBattles) candidates.Add(name);
                }
                if (savedCountChanged) PlayerPrefs.Save();
            } catch (PlayerPrefsException exception) {
                Debug.LogWarning($"[PlayerFeedbackPresenter] {exception.Message}");
            }
            return candidates.ToArray();
        }

        internal async Task PresentAsync(string[] candidates) {
            if (candidates.Length == 0) return;
            using var cancellation = new CancellationTokenSource();
            presentation = cancellation;
            var token = cancellation.Token;
            try {
                using var screenHold = focus.HoldScreen();
                using var dialog = new ChoiceDialogPresenter(view, pads, focus, modalPriority: 2);
                using var closeOnCancellation = token.Register(dialog.Dispose);
                foreach (var name in candidates) {
                    if (await api.HasFeedbackAsync(name, token)) {
                        submittedNames.Add(name);
                        continue;
                    }
                    token.ThrowIfCancellationRequested();
                    await dialog.AskAsync("感想のお願い",
                        $"{name} さん、プレイありがとうございます\nよろしければ、感想をお聞かせください\nお名前と感想は共有され、送信後は変更できません\n空欄でも送信できます",
                        new[] { "送信" }, "", directInput: true);
                    await SubmitAsync(name, dialog, token);
                }
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                Debug.LogWarning($"[PlayerFeedbackPresenter] {exception.Message}");
            } finally {
                if (presentation == cancellation) presentation = null;
            }
        }

        async Task SubmitAsync(string name, ChoiceDialogPresenter dialog, CancellationToken token) {
            var comment = dialog.InputText;
            while (true) {
                try {
                    await api.SubmitFeedbackAsync(name, comment, token);
                    break;
                } catch (ApiRequestException exception) when (exception.StatusCode == 409) {
                    break;
                } catch (ApiRequestException exception) {
                    Debug.LogWarning($"[PlayerFeedbackPresenter] {exception.Message}");
                }
                token.ThrowIfCancellationRequested();
                var choice = await dialog.AskAsync("感想を送信できませんでした", "通信環境を確認して再試行できます",
                    new[] { "再試行", "閉じる" }, directInput: true);
                if (choice != 0) return;
            }
            submittedNames.Add(name);
        }

        public void Dispose() {
            subscriptions.Dispose();
            presentation?.Cancel();
        }
    }
}
