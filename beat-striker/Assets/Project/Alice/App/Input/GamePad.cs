


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
    public class GamePad : MonoBehaviour, IGamePad {
        IGamePadRegistry registry;
        private PlayerInput playerInput;
        InputAction directionAction;
        InputAction northAction;
        InputAction westAction;
        InputAction southAction;
        InputAction eastAction;
        InputAction leftAction;
        InputAction rightAction;
        InputAction startAction;
        InputAction selectAction;
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
            playerInput = GetComponent<PlayerInput>();
            CacheActions();
            DontDestroyOnLoad(this.gameObject);
        }

        public void Initialize(IGamePadRegistry registry) {
            this.registry = registry;

            if (isActiveAndEnabled) {
                RegisterIfNeeded();
            }
        }

        void OnEnable() {
            SubscribeActions();
            playerInput.onControlsChanged += OnControlsChanged;

            RegisterIfNeeded();
        }

        void OnDisable() {
            UnsubscribeActions();
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
            if (changed != playerInput) {
                return;
            }

            UnsubscribeActions();
            CacheActions();
            SubscribeActions();
        }

        void OnDestroy() {
            onDirection.Dispose();
            onDirectionCanceled.Dispose();
            onButtonDown.Dispose();
            onButtonUp.Dispose();
        }

        void CacheActions() {
            var actions = playerInput.actions;
            directionAction = actions.FindAction("Direction", true);
            northAction = actions.FindAction("North", true);
            westAction = actions.FindAction("West", true);
            southAction = actions.FindAction("South", true);
            eastAction = actions.FindAction("East", true);
            leftAction = actions.FindAction("Left", true);
            rightAction = actions.FindAction("Right", true);
            startAction = actions.FindAction("Start", true);
            selectAction = actions.FindAction("Select", true);
        }

        void SubscribeActions() {
            directionAction.started += OnDirection;
            directionAction.performed += OnDirection;
            directionAction.canceled += OnDirection;

            northAction.started += OnNorth;
            northAction.canceled += OnNorth;
            westAction.started += OnWest;
            westAction.canceled += OnWest;
            southAction.started += OnSouth;
            southAction.canceled += OnSouth;
            eastAction.started += OnEast;
            eastAction.canceled += OnEast;
            leftAction.started += OnLeft;
            leftAction.canceled += OnLeft;
            rightAction.started += OnRight;
            rightAction.canceled += OnRight;
            startAction.started += OnStart;
            startAction.canceled += OnStart;
            selectAction.started += OnSelect;
            selectAction.canceled += OnSelect;
        }

        void UnsubscribeActions() {
            directionAction.started -= OnDirection;
            directionAction.performed -= OnDirection;
            directionAction.canceled -= OnDirection;

            northAction.started -= OnNorth;
            northAction.canceled -= OnNorth;
            westAction.started -= OnWest;
            westAction.canceled -= OnWest;
            southAction.started -= OnSouth;
            southAction.canceled -= OnSouth;
            eastAction.started -= OnEast;
            eastAction.canceled -= OnEast;
            leftAction.started -= OnLeft;
            leftAction.canceled -= OnLeft;
            rightAction.started -= OnRight;
            rightAction.canceled -= OnRight;
            startAction.started -= OnStart;
            startAction.canceled -= OnStart;
            selectAction.started -= OnSelect;
            selectAction.canceled -= OnSelect;
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