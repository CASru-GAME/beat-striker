


using System.Collections.Generic;
using App;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alice {

    public enum GamePadButton {
        North,
        South,
        West,
        East,
        Right,
        Left,
        Start,
        Select,
    }

    [RequireComponent(typeof(PlayerInput))]
    public class GamePad : MonoBehaviour, GameInput.IPlayerActions, IGamePad {
        IGamePadRegistry registry;
        private PlayerInput playerInput;
        private GameInput input;
        bool isRegistered;
        readonly Subject<Vector2> onDirection = new();
        readonly Subject<Unit> onDirectionCanceled = new();
        readonly Subject<GamePadButton> onButtonDown = new();
        readonly Subject<GamePadButton> onButtonUp = new();

        public Observable<Vector2> OnDirectionAsObservable => onDirection;
        public Observable<Unit> OnDirectionCanceledAsObservable => onDirectionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => onButtonDown;
        public Observable<GamePadButton> OnButtonUpAsObservable => onButtonUp;
        public string DeviceName => playerInput.currentControlScheme;

        void Awake() {
            input = new GameInput();
            playerInput = GetComponent<PlayerInput>();
            DontDestroyOnLoad(this.gameObject);
        }

        public void Initialize(IGamePadRegistry registry) {
            this.registry = registry;

            if (isActiveAndEnabled) {
                RegisterIfNeeded();
            }
        }

        void OnEnable() {
            input.asset.devices = playerInput.devices;
            input.Player.AddCallbacks(this);
            input.Player.Enable();
            playerInput.onControlsChanged += OnControlsChanged;

            RegisterIfNeeded();
        }

        void OnDisable() {
            input.Player.RemoveCallbacks(this);
            input.Player.Disable();
            playerInput.onControlsChanged -= OnControlsChanged;

            if (isRegistered) {
                registry.RequestUnregister(this);
                isRegistered = false;
            }
        }

        void RegisterIfNeeded() {
            if(isRegistered) return;
            if (registry == null) {
                Debug.LogWarning("GamePad registry is not available. GamePad will not be registered.", this);
                return;
            }

            registry.RequestRegister(this);
            isRegistered = true;
        }

        private void OnControlsChanged(PlayerInput changed) {
            if (changed == playerInput)
                input.asset.devices = playerInput.devices;
        }

        void OnDestroy() {
            input.Dispose();
            onDirection.Dispose();
            onDirectionCanceled.Dispose();
            onButtonDown.Dispose();
            onButtonUp.Dispose();
        }

        public void OnDirection(InputAction.CallbackContext c) {
            onDirection.OnNext(c.ReadValue<Vector2>());
            if (c.canceled) {
                onDirectionCanceled.OnNext(Unit.Default);
            }
        }

        public void OnNorth(InputAction.CallbackContext c) {
            EmitButton(c, GamePadButton.North);
        }

        public void OnWest(InputAction.CallbackContext c) {
            EmitButton(c, GamePadButton.West);
        }

        public void OnSouth(InputAction.CallbackContext c) {
            EmitButton(c, GamePadButton.South);
        }

        public void OnEast(InputAction.CallbackContext c) {
            EmitButton(c, GamePadButton.East);
        }

        public void OnLeft(InputAction.CallbackContext c) {
            EmitButton(c, GamePadButton.Left);
        }

        public void OnRight(InputAction.CallbackContext c) {
            EmitButton(c, GamePadButton.Right);
        }

        public void OnStart(InputAction.CallbackContext c) {
            EmitButton(c, GamePadButton.Start);
        }

        public void OnSelect(InputAction.CallbackContext c) {
            EmitButton(c, GamePadButton.Select);
        }

        void EmitButton(InputAction.CallbackContext c, GamePadButton button) {
            if (c.started) {
                onButtonDown.OnNext(button);
            }
            else if (c.canceled) {
                onButtonUp.OnNext(button);
            }
        }
    }
}