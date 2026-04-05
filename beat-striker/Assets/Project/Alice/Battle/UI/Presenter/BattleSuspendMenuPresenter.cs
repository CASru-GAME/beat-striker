using Core;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class BattleSuspendMenuPresenter : MonoBehaviour {
        [Inject] ICursorDeployer cursorDeployer;
        [SerializeField] GameObject root;
        [SerializeField] Botan suspendButton, resumeButton;
        [SerializeField] float showStartScale = 0.9f;
        [SerializeField] float showScaleDuration = 0.18f;
        [SerializeField] float hideEndScale = 0.9f;
        [SerializeField] float hideScaleDuration = 0.14f;

        readonly Subject<Unit> suspendRequestedSubject = new();
        readonly Subject<Unit> resumeRequestedSubject = new();

        public Observable<Unit> OnSuspendRequested => suspendRequestedSubject;
        public Observable<Unit> OnResumeRequested => resumeRequestedSubject;

        void Awake() {
            EnsureDependenciesInjected();
        }

        void Start() {
            suspendButton.onClick += e => {
                RequestSuspend();
            };

            resumeButton.onClick += e => {
                RequestResume();
            };

            HideImmediate();
        }

        void OnDestroy() {
            cursorDeployer.SetForceEnabled(false);
            suspendRequestedSubject.Dispose();
            resumeRequestedSubject.Dispose();
        }

        public void Show() {
            LeanTween.cancel(root);
            root.transform.localScale = Vector3.one * showStartScale;
            root.SetActive(true);
            LeanTween.scale(root, Vector3.one, showScaleDuration)
                .setEaseOutBack();
            cursorDeployer.SetForceEnabled(true);
        }

        public void Hide() {
            if (!root.activeSelf) {
                return;
            }

            LeanTween.cancel(root);
            LeanTween.scale(root, Vector3.one * hideEndScale, hideScaleDuration)
                .setEaseInBack()
                .setOnComplete(() => {
                    root.SetActive(false);
                    root.transform.localScale = Vector3.one;
                });
            cursorDeployer.SetForceEnabled(false);
        }

        void HideImmediate() {
            LeanTween.cancel(root);
            root.transform.localScale = Vector3.one;
            root.SetActive(false);
            cursorDeployer.SetForceEnabled(false);
        }

        void RequestSuspend() {
            suspendRequestedSubject.OnNext(Unit.Default);
        }

        void RequestResume() {
            resumeRequestedSubject.OnNext(Unit.Default);
        }

        void EnsureDependenciesInjected() {
            if (cursorDeployer != null) {
                return;
            }

            var battleScope = LifetimeScope.Find<BattleScope>(gameObject.scene);
            if (battleScope != null && battleScope.Container != null) {
                battleScope.Container.Inject(this);
                return;
            }

            var appScope = LifetimeScope.Find<AppScope>();
            if (appScope != null && appScope.Container != null) {
                appScope.Container.Inject(this);
            }
        }
    }
}
