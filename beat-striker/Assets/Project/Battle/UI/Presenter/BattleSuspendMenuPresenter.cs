using Core;
using System;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class BattleSuspendMenuPresenter : IDisposable {
        readonly ICursorDeployer cursorDeployer;
        readonly IGamePadRegistry gamePads;
        readonly IDisposable focusRegistration;
        bool opening, closing, requested;
        readonly BattleSuspendMenuView suspendMenuView;
        readonly IDisposable suspendSubscription;
        readonly IDisposable resumeSubscription;
        readonly IDisposable backSubscription;

        readonly Subject<int?> suspendRequestedSubject = new();
        readonly Subject<Unit> resumeRequestedSubject = new();

        public Observable<int?> OnSuspendRequested => suspendRequestedSubject;
        public Observable<Unit> OnResumeRequested => resumeRequestedSubject;

        [Inject]
        public BattleSuspendMenuPresenter(ICursorDeployer cursorDeployer, BattleSuspendMenuView suspendMenuView, IInputFocusService focus, IGamePadRegistry gamePads) {
            this.cursorDeployer = cursorDeployer;
            this.gamePads = gamePads;
            focusRegistration = focus.RegisterModal(suspendMenuView.Root.transform);
            this.suspendMenuView = suspendMenuView;
            suspendSubscription = suspendMenuView.SuspendButton.OnClickEvent.Subscribe(RequestSuspend);
            resumeSubscription = suspendMenuView.ResumeButton.OnClickEvent.Subscribe(_ => RequestResume());
            backSubscription = gamePads.OnAnyButtonDown
                .Where(input => input.Button == GamePadButton.South && focus.CanReceive(suspendMenuView.Root.transform, input.PlayerId))
                .Subscribe(_ => {
                    if (opening || closing || requested) return;
                    suspendMenuView.ResumeButton.PlayClickSound();
                    RequestResume();
                });

            HideImmediate();
        }

        public void Dispose() {
            focusRegistration.Dispose();
            suspendSubscription.Dispose();
            resumeSubscription.Dispose();
            backSubscription.Dispose();
            cursorDeployer.SetForceEnabled(false);
            suspendRequestedSubject.Dispose();
            resumeRequestedSubject.Dispose();
        }

        public void Show(int commandPlayerId) {
            string ButtonName(GamePadButton button, string touchLabel) =>
                gamePads.GetButtonDisplayName(commandPlayerId, button) ?? touchLabel;

            suspendMenuView.SetCommandButtons(
                ButtonName(GamePadButton.West, "Y"),
                ButtonName(GamePadButton.East, "A"),
                ButtonName(GamePadButton.South, "B"),
                ButtonName(GamePadButton.North, "X"),
                ButtonName(GamePadButton.Start, "Start"));
            opening = true; closing = false; requested = false;
            LeanTween.cancel(suspendMenuView.Root);
            suspendMenuView.Root.transform.localScale = Vector3.one * suspendMenuView.ShowStartScale;
            suspendMenuView.Root.SetActive(true);
            LeanTween.scale(suspendMenuView.Root, Vector3.one, suspendMenuView.ShowScaleDuration)
                .setEaseOutBack().setOnComplete(() => opening = false);
            cursorDeployer.SetForceEnabled(true);
        }

        public void Hide() {
            if (closing) return;
            closing = true;
            if (!suspendMenuView.Root.activeSelf) {
                return;
            }

            LeanTween.cancel(suspendMenuView.Root);
            LeanTween.scale(suspendMenuView.Root, Vector3.one * suspendMenuView.HideEndScale, suspendMenuView.HideScaleDuration)
                .setEaseInBack()
                .setOnComplete(() => {
                    suspendMenuView.Root.SetActive(false);
                    suspendMenuView.Root.transform.localScale = Vector3.one;
                });
            cursorDeployer.SetForceEnabled(false);
        }

        void HideImmediate() {
            LeanTween.cancel(suspendMenuView.Root);
            suspendMenuView.Root.transform.localScale = Vector3.one;
            suspendMenuView.Root.SetActive(false);
            cursorDeployer.SetForceEnabled(false);
        }

        void RequestSuspend(BotanEventData data) {
            if (opening || closing || requested) return;
            requested = true;
            suspendRequestedSubject.OnNext(data.EventData is CursorPointerEventData cursor && cursor.PlayerId is 0 or 1
                ? cursor.PlayerId : null);
        }

        void RequestResume() {
            if (opening || closing || requested) return;
            requested = true;
            resumeRequestedSubject.OnNext(Unit.Default);
        }
    }
}
