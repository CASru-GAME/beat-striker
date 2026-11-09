


using System.Collections.Generic;
using App;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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
        GamePadButtonMapping buttonMapping;
        Gamepad pairedGamepad;
        private PlayerInput playerInput;
        private GameInput input;
        bool isRegistered;
        bool waitingForButtonRelease;
        readonly Subject<Vector2> onDirection = new();
        readonly Subject<Unit> onDirectionCanceled = new();
        readonly Subject<GamePadButton> onButtonDown = new();
        readonly Subject<GamePadButton> onButtonUp = new();

        public Observable<Vector2> OnDirectionAsObservable => onDirection;
        public Observable<Unit> OnDirectionCanceledAsObservable => onDirectionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => onButtonDown;
        public Observable<GamePadButton> OnButtonUpAsObservable => onButtonUp;
        public string DeviceName => playerInput.currentControlScheme;

        internal string GetButtonDisplayName(GamePadButton button) {
            var actions = input.Player;
            var action = button switch {
                GamePadButton.North => actions.North,
                GamePadButton.South => actions.South,
                GamePadButton.West => actions.West,
                GamePadButton.East => actions.East,
                GamePadButton.Left => actions.Left,
                GamePadButton.Right => actions.Right,
                GamePadButton.Start => actions.Start,
                GamePadButton.Select => actions.Select,
                _ => throw new System.ArgumentOutOfRangeException(nameof(button)),
            };

            if (action.controls.Count == 0) return null;
            var control = action.controls[0];
            return control.shortDisplayName ?? control.displayName;
        }

        public void DestroyGamePad() {
            Destroy(gameObject);
        }

        void Awake() {
            input = new GameInput();
            playerInput = GetComponent<PlayerInput>();
            DontDestroyOnLoad(this.gameObject);
        }

        public void Initialize(IGamePadRegistry registry, GamePadButtonMapping buttonMapping) {
            this.registry = registry;
            this.buttonMapping = buttonMapping;
            buttonMapping.Changed.Subscribe(device => {
                if (isActiveAndEnabled && pairedGamepad == device) {
                    SyncControls();
                }
            }).AddTo(this);

            if (isActiveAndEnabled && input != null) SyncControls();
        }

        void OnEnable() {
            input.Player.AddCallbacks(this);
            playerInput.onControlsChanged += OnControlsChanged;
            SyncControls();
        }

        void OnDisable() {
            waitingForButtonRelease = false;
            input.Player.RemoveCallbacks(this);
            input.Player.Disable();
            playerInput.onControlsChanged -= OnControlsChanged;

            if (isRegistered) {
                registry.RequestUnregister(this);
                isRegistered = false;
            }
        }

        internal void Register(int playerId) {
            registry.RequestRegister(playerId, this);
            isRegistered = true;
        }

        internal void SwitchDevice(string controlScheme, InputDevice device) {
            playerInput.SwitchCurrentControlScheme(controlScheme, device);
            SyncControls();
        }

        private void OnControlsChanged(PlayerInput changed) {
            if (changed == playerInput)
                SyncControls();
        }

        void SyncControls() {
            waitingForButtonRelease = false;
            input.Player.Disable();
            input.asset.devices = playerInput.devices;
            pairedGamepad = null;
            foreach (var device in playerInput.devices) {
                if (device is Gamepad gamepad) {
                    pairedGamepad = gamepad;
                    break;
                }
            }
            var scheme = playerInput.currentControlScheme;
            buttonMapping?.Apply(input.Player, pairedGamepad, scheme);
            if (string.IsNullOrEmpty(scheme)) return;
            input.bindingMask = InputBinding.MaskByGroup(scheme);
            waitingForButtonRelease = true;
        }

        void Update() {
            if (!waitingForButtonRelease) return;

            // Joining happens before the joining event is applied to the device state.
            // Wait until a later frame and release so that press cannot also click the UI.
            foreach (var action in input.Player.Get()) {
                foreach (var control in action.controls) {
                    if (control is ButtonControl button && button.isPressed) return;
                }
            }

            waitingForButtonRelease = false;
            input.Player.Enable();
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
                if (c.control.device is Gamepad && buttonMapping?.BlocksGamepadButtons == true) return;
                onButtonDown.OnNext(button);
            }
            else if (c.canceled) {
                onButtonUp.OnNext(button);
            }
        }
    }
}
