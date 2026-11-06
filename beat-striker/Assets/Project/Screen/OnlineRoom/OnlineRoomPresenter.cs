using System;
using System.Threading.Tasks;
using R3;
using UnityEngine;

namespace Alice {
    public class OnlineRoomPresenter : IDisposable {
        readonly OnlineRoomView view;
        readonly IOnlineRoomClient room;
        readonly IOnlineDuelFusionClient duel;
        readonly ISceneTransitionService transition;
        readonly CompositeDisposable subscriptions = new();
        string selection = "";
        string feedback = "";
        bool busy;
        bool refreshing;
        bool disposed;
        bool leaving;
        int operation;

        public OnlineRoomPresenter(OnlineRoomView view, IOnlineRoomClient room, IOnlineDuelFusionClient duel, ISceneTransitionService transition, IInputFocusService focus, IGamePadRegistry gamePads) {
            this.view = view;
            view.InputRaycaster.Initialize(focus);
            view.DuelRaycaster.Initialize(focus);
            focus.RegisterModal(view.DuelRoot, isActive: () => view.IsDuelPending).AddTo(subscriptions);
            Observable.EveryUpdate().Subscribe(_ => view.InputAllowed = focus.CanPoint(view.transform)).AddTo(subscriptions);
            focus.Changed.Subscribe(_ => view.InputAllowed = focus.CanPoint(view.transform)).AddTo(subscriptions);
            this.room = room;
            this.duel = duel;
            this.transition = transition;
            view.BackRequested.Subscribe(_ => Back()).AddTo(subscriptions);
            gamePads.OnAnyButtonDown.Where(input => input.Button == GamePadButton.South).Subscribe(input => {
                if (focus.CanReceive(null, input.PlayerId)) {
                    if (disposed || leaving || duel.State.CurrentValue.HasReservation) return;
                    view.PlayBackSound();
                    Back();
                }
                else if (focus.CanReceive(view.DuelRoot, input.PlayerId)) {
                    if (busy || disposed || leaving) return;
                    switch (duel.State.CurrentValue.UiMode) {
                        case OnlineDuelUiMode.IncomingInvite: view.PlayRejectSound(); Respond(duel.RejectInvite); break;
                        case OnlineDuelUiMode.InviteSent: view.PlayCancelSound(); Respond(duel.CancelInvite); break;
                    }
                }
            }).AddTo(subscriptions);
            view.RefreshRequested.Subscribe(ignored => { _ = RefreshAsync(); }).AddTo(subscriptions);
            view.PlayerSelected.Subscribe(id => { if (busy) return; selection = id; feedback = ""; Render(); }).AddTo(subscriptions);
            view.ChallengeRequested.Subscribe(ignored => { _ = ChallengeAsync(); }).AddTo(subscriptions);
            view.AcceptRequested.Subscribe(_ => Respond(duel.AcceptInvite)).AddTo(subscriptions);
            view.RejectRequested.Subscribe(_ => Respond(duel.RejectInvite)).AddTo(subscriptions);
            view.CancelRequested.Subscribe(_ => Respond(duel.CancelInvite)).AddTo(subscriptions);
            room.Roster.Subscribe(roster => {
                if (!Array.Exists(roster.Players, p => p.sessionId == selection)) selection = "";
                Render();
            }).AddTo(subscriptions);
            duel.State.Subscribe(state => {
                busy = false;
                operation++;
                feedback = LocalizeMessage(state.Message);
                Render();
            }).AddTo(subscriptions);
            _ = EnterAsync();
        }

        async Task EnterAsync() {
            await transition.RequestEndTransitionAsync(AppScene.OnlineRoom);
            if (!disposed) await RefreshAsync();
        }

        async Task RefreshAsync() {
            if (disposed || leaving || refreshing) return;
            refreshing = true;
            feedback = "接続を確認しています…";
            Render();
            try {
                await room.RefreshRoomAsync();
                if (disposed || leaving) return;
                feedback = "";
                Render();
                await Task.Delay(12000);
                if (disposed || leaving || room.Roster.CurrentValue.IsConnected) return;
                feedback = "一覧を取得できませんでした。再接続を押してください";
                Render();
            } catch (Exception exception) {
                if (disposed || leaving) return;
                Debug.LogWarning($"[OnlineRoom] Connection failed: {exception.Message}");
                feedback = "接続できませんでした。再接続を押してください";
                Render();
            } finally { refreshing = false; }
        }

        async Task ChallengeAsync() {
            if (busy || disposed || leaving) return;
            busy = true;
            feedback = "";
            var current = ++operation;
            Render();
            try {
                if (!await room.InvitePlayerAsync(selection)) {
                    if (disposed || leaving || current != operation) return;
                    busy = false;
                    feedback = "この相手には挑戦できません。選び直してください";
                    Render();
                    return;
                }
                await CheckResponseAsync(current);
            } catch (Exception exception) {
                if (disposed || leaving || current != operation) return;
                Debug.LogWarning($"[OnlineRoom] Challenge failed: {exception.Message}");
                busy = false;
                feedback = "送信できませんでした。再接続してください";
                Render();
            }
        }

        void Respond(Action action) {
            if (busy || disposed || leaving) return;
            busy = true;
            var current = ++operation;
            action();
            Render();
            _ = CheckResponseAsync(current);
        }

        async Task CheckResponseAsync(int current) {
            await Task.Delay(8000);
            if (disposed || leaving || current != operation || !busy) return;
            busy = false;
            feedback = "返答を確認できませんでした。再接続してください";
            Render();
        }

        void Back() {
            if (disposed || leaving || duel.State.CurrentValue.HasReservation) return;
            var result = transition.RequestStartTransition(AppScene.Title);
            if (!result.IsSuccess) return;
            leaving = true;
            var mode = duel.State.CurrentValue.UiMode;
            if (mode == OnlineDuelUiMode.IncomingInvite) duel.RejectInvite();
            else if (mode == OnlineDuelUiMode.InviteSent) duel.CancelInvite();
        }

        void Render() {
            if (!disposed && !leaving) view.Render(room.Roster.CurrentValue, duel.State.CurrentValue, selection, busy, feedback);
        }

        static string LocalizeMessage(string message) => message switch {
            "Invite rejected." => "今回は見送り。また別の相手に挑もう！",
            "Invite canceled." => "挑戦状が取り消されました",
            "Invite expired." => "挑戦状の有効時間が切れました",
            "Opponent disconnected." or "Presence expired." => "相手が退出しました",
            "Target player is not available." => "相手が取り込み中になりました。選び直してください",
            _ => "",
        };

        public void Dispose() {
            disposed = true;
            operation++;
            subscriptions.Dispose();
        }
    }
}
