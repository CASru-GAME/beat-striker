using Core;
using System;
using R3;
using UnityEngine;

namespace Alice {
    public class BattleSuspendMenuPresenter : IDisposable {
        readonly ICursorDeployer cursorDeployer;
        readonly BattleSuspendMenuView suspendMenuView;
        readonly Action<BotanEventData> onSuspendButtonClicked;
        readonly Action<BotanEventData> onResumeButtonClicked;

        readonly Subject<Unit> suspendRequestedSubject = new();
        readonly Subject<Unit> resumeRequestedSubject = new();

        public Observable<Unit> OnSuspendRequested => suspendRequestedSubject;
        public Observable<Unit> OnResumeRequested => resumeRequestedSubject;

        public BattleSuspendMenuPresenter(ICursorDeployer cursorDeployer, BattleSuspendMenuView suspendMenuView) {
            this.cursorDeployer = cursorDeployer;
            this.suspendMenuView = suspendMenuView;
            onSuspendButtonClicked = _ => RequestSuspend();
            onResumeButtonClicked = _ => RequestResume();
            suspendMenuView.SuspendButton.onClick += onSuspendButtonClicked;
            suspendMenuView.ResumeButton.onClick += onResumeButtonClicked;

            HideImmediate();
        }

        public void Dispose() {
            suspendMenuView.SuspendButton.onClick -= onSuspendButtonClicked;
            suspendMenuView.ResumeButton.onClick -= onResumeButtonClicked;
            cursorDeployer.SetForceEnabled(false);
            suspendRequestedSubject.Dispose();
            resumeRequestedSubject.Dispose();
        }

        public void Show() {
            LeanTween.cancel(suspendMenuView.Root);
            suspendMenuView.Root.transform.localScale = Vector3.one * suspendMenuView.ShowStartScale;
            suspendMenuView.Root.SetActive(true);
            LeanTween.scale(suspendMenuView.Root, Vector3.one, suspendMenuView.ShowScaleDuration)
                .setEaseOutBack();
            cursorDeployer.SetForceEnabled(true);
        }

        public void Hide() {
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

        void RequestSuspend() {
            suspendRequestedSubject.OnNext(Unit.Default);
        }

        void RequestResume() {
            resumeRequestedSubject.OnNext(Unit.Default);
        }
    }
}
