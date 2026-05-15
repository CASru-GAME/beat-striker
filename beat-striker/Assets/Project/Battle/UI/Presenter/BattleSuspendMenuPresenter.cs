using Core;
using System;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class BattleSuspendMenuPresenter : IDisposable {
        readonly ICursorDeployer cursorDeployer;
        readonly BattleSuspendMenuView suspendMenuView;
        readonly IDisposable suspendSubscription;
        readonly IDisposable resumeSubscription;

        readonly Subject<Unit> suspendRequestedSubject = new();
        readonly Subject<Unit> resumeRequestedSubject = new();

        public Observable<Unit> OnSuspendRequested => suspendRequestedSubject;
        public Observable<Unit> OnResumeRequested => resumeRequestedSubject;

        [Inject]
        public BattleSuspendMenuPresenter(ICursorDeployer cursorDeployer, BattleSuspendMenuView suspendMenuView) {
            this.cursorDeployer = cursorDeployer;
            this.suspendMenuView = suspendMenuView;
            suspendSubscription = suspendMenuView.SuspendButton.OnClickEvent.Subscribe(_ => RequestSuspend());
            resumeSubscription = suspendMenuView.ResumeButton.OnClickEvent.Subscribe(_ => RequestResume());

            HideImmediate();
        }

        public void Dispose() {
            suspendSubscription.Dispose();
            resumeSubscription.Dispose();
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
