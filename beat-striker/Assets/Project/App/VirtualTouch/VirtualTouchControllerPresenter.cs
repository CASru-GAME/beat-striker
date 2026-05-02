using System;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class VirtualTouchControllerPresenter : IInitializable, IDisposable {
        readonly IAppUISetting appUISetting;
        readonly IGamePadRegistry gamePadRegistry;
        readonly VirtualTouchControllerCanvasView view;

        readonly CompositeDisposable disposables = new();
        readonly VirtualTouchGamePad virtualTouchGamePad;
        bool isRegistered;
        bool isTouchControllerEnabled;

        [Inject]
        public VirtualTouchControllerPresenter(
            IAppUISetting appUISetting,
            IGamePadRegistry gamePadRegistry,
            VirtualTouchControllerCanvasView view) {
            this.appUISetting = appUISetting;
            this.gamePadRegistry = gamePadRegistry;
            this.view = view;
            virtualTouchGamePad = new VirtualTouchGamePad(OnVirtualTouchGamePadDestroyed);
        }

        public void Initialize() {
            view.OnDirectionChanged.Subscribe(EmitDirection).AddTo(disposables);
            view.OnDirectionCanceled.Subscribe(_ => CancelDirection()).AddTo(disposables);
            view.OnButtonDown.Subscribe(EmitButtonDown).AddTo(disposables);
            view.OnButtonUp.Subscribe(EmitButtonUp).AddTo(disposables);

            appUISetting.UsesTouchController.Subscribe(ApplyTouchControllerState).AddTo(disposables);
            ApplyTouchControllerState(appUISetting.UsesTouchController.CurrentValue);
        }

        void ApplyTouchControllerState(bool enabled) {
            isTouchControllerEnabled = enabled;
            view.SetVisible(enabled);

            if (enabled) {
                RegisterIfNeeded();
                return;
            }

            UnregisterIfNeeded();
        }

        void RegisterIfNeeded() {
            if (isRegistered) {
                return;
            }
            if (!isTouchControllerEnabled) {
                return;
            }

            gamePadRegistry.RequestRegister(virtualTouchGamePad);
            isRegistered = true;
        }

        void UnregisterIfNeeded() {
            if (!isRegistered) {
                return;
            }

            gamePadRegistry.RequestUnregister(virtualTouchGamePad);
            isRegistered = false;
        }

        void EmitDirection(Vector2 direction) {
            RegisterIfNeeded();
            virtualTouchGamePad.EmitDirection(direction);
        }

        void CancelDirection() {
            virtualTouchGamePad.CancelDirection();
        }

        void EmitButtonDown(GamePadButton button) {
            RegisterIfNeeded();
            virtualTouchGamePad.EmitButtonDown(button);
        }

        void EmitButtonUp(GamePadButton button) {
            virtualTouchGamePad.EmitButtonUp(button);
        }

        void OnVirtualTouchGamePadDestroyed() {
            isRegistered = false;
        }

        public void Dispose() {
            UnregisterIfNeeded();
            disposables.Dispose();
            virtualTouchGamePad.Dispose();
        }
    }
}
