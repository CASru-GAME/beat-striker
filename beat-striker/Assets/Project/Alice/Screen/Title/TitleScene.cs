using R3;
using UnityEngine;
using Alice;
using System;

namespace Alice {
    public class TitleScene : MonoBehaviour {
        readonly Subject<Unit> gotoSelectRequested = new();
        readonly Subject<Unit> openSettingsRequested = new();
        readonly Subject<Unit> quitRequested = new();

        [SerializeField] SettingsDialogScope settingsDialogScopePrefab;
        [SerializeField] Transform settingsDialogParent;
        [SerializeField] ActionEmitter settingsEmitter;
        [SerializeField] ActionEmitter quitEmitter;

        SettingsDialogScope settingsDialogScope;

        public Observable<Unit> GotoSelectRequested => gotoSelectRequested;
        public Observable<Unit> GotoSettingsRequested => openSettingsRequested;
        public Observable<Unit> QuitRequested => quitRequested;

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
            if (settingsEmitter != null) {
                settingsEmitter.OnClickEvent.Subscribe(_ => RequestOpenSettings()).AddTo(this);
            }

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
    }
}
