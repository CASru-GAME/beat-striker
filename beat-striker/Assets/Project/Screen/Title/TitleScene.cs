using R3;
using UnityEngine;
using Alice;

namespace Alice {
    public class TitleScene : MonoBehaviour {
        readonly Subject<Unit> gotoSelectRequested = new();
        readonly Subject<Unit> openSettingsRequested = new();
        readonly Subject<Unit> quitRequested = new();
        readonly Subject<Unit> tutorialBattleAccepted = new();
        readonly Subject<Unit> tutorialBattleDeclined = new();

        [SerializeField] SettingsDialogScope settingsDialogScopePrefab;
        [SerializeField] Transform settingsDialogParent;
        [SerializeField] TutorialStartDialog tutorialStartDialogPrefab;
        [SerializeField] Transform tutorialStartDialogParent;
        [SerializeField] ActionEmitter settingsEmitter;
        [SerializeField] ActionEmitter quitEmitter;

        SettingsDialogScope settingsDialogScope;
        TutorialStartDialog tutorialStartDialog;
        bool tutorialDialogBound;

        public Observable<Unit> GotoSelectRequested => gotoSelectRequested;
        public Observable<Unit> GotoSettingsRequested => openSettingsRequested;
        public Observable<Unit> QuitRequested => quitRequested;
        public Observable<Unit> TutorialBattleAccepted => tutorialBattleAccepted;
        public Observable<Unit> TutorialBattleDeclined => tutorialBattleDeclined;

        public void RequestGotoSelectScene() {
            gotoSelectRequested.OnNext(Unit.Default);
        }

        // Called by UI Button (OnClick) or presenter via RequestOpenSettings
        public void RequestOpenSettings() {
            openSettingsRequested.OnNext(Unit.Default);
        }

        // Called by UI Button (OnClick) or ActionEmitter
        public void RequestQuitGame() {
            quitRequested.OnNext(Unit.Default);
        }

        void Awake() {
            settingsEmitter.OnClickEvent.Subscribe(_ => RequestOpenSettings()).AddTo(this);
            quitEmitter.OnClickEvent.Subscribe(_ => RequestQuitGame()).AddTo(this);
        }

        public void OpenSettingsDialog() {
            if (settingsDialogScope == null) {
                settingsDialogScope = Instantiate(settingsDialogScopePrefab, settingsDialogParent);
            }

            settingsDialogScope.gameObject.SetActive(true);
        }

        public void CloseSettingsDialog() {
            if (settingsDialogScope == null) {
                return;
            }

            settingsDialogScope.gameObject.SetActive(false);
        }

        public void OpenTutorialStartDialog() {
            if (tutorialStartDialog == null) {
                tutorialStartDialog = Instantiate(tutorialStartDialogPrefab, tutorialStartDialogParent);
            }

            BindTutorialDialogIfNeeded();
            tutorialStartDialog.SetVisible(true);
        }

        public void CloseTutorialStartDialog() {
            if (tutorialStartDialog == null) {
                return;
            }

            tutorialStartDialog.SetVisible(false);
        }

        void BindTutorialDialogIfNeeded() {
            if (tutorialDialogBound) {
                return;
            }

            tutorialStartDialog.YesRequested
                .Subscribe(_ => {
                    tutorialStartDialog.SetVisible(false);
                    tutorialBattleAccepted.OnNext(Unit.Default);
                })
                .AddTo(this);

            tutorialStartDialog.NoRequested
                .Subscribe(_ => {
                    tutorialStartDialog.SetVisible(false);
                    tutorialBattleDeclined.OnNext(Unit.Default);
                })
                .AddTo(this);

            tutorialDialogBound = true;
        }
    }
}
