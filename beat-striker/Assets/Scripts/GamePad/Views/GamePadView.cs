

using Core.GamePad.Models;
using Core.GamePad.Types;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.GamePad.Views {
    [RequireComponent(typeof(PlayerInput))]
    public sealed class GamePadView : MonoBehaviour, GameInput.IPlayerActions {
        private PlayerInput playerInput;
        private GameInput input;
        private IGamePadModel model;
        private ILifeMutater lifeMutater;

        public void Construct(IGamePadModel model, ILifeMutater lifeMutater) {
            this.model = model;
            this.lifeMutater = lifeMutater;

            // Re-link lifecycle if needed, or Model does it via Scope?
            // Presenter used to link life.
            // Model OnEnable/Disable is called by Scope via Life usually, OR we link it here?
            // Presenter did: life.Link(OnEnable, OnDisable).
            // Let's assume Scope handles linking Model to Life, OR View does it?
            // View Construct takes lifeMutater.
            // Let's assume Scope links Model to Life.
            // But View receives Unity events.
        }

        void Awake() {
            input = new GameInput();
            playerInput = GetComponent<PlayerInput>();
        }

        // View Lifecycle handles Unity Input
        void OnEnable() {
            input.asset.devices = playerInput.devices;
            input.Player.AddCallbacks(this);
            input.Player.Enable();
            playerInput.onControlsChanged += OnControlsChanged;
            lifeMutater?.SetEnable(true);
        }

        void OnDisable() {
            input.Player.RemoveCallbacks(this);
            input.Player.Disable();
            playerInput.onControlsChanged -= OnControlsChanged;
            lifeMutater.SetEnable(false);
        }

        private void OnControlsChanged(PlayerInput changed) {
            if (changed == playerInput)
                input.asset.devices = playerInput.devices;
        }

        void OnDestroy() => input.Dispose();

        public void OnDirection(InputAction.CallbackContext c)
            => model.HandleDirection(c.ReadValue<Vector2>());

        public void OnNorth(InputAction.CallbackContext c) {
            if (c.started) model.HandleButton(GamePadButton.North, GamePadAction.Down);
            else if (c.canceled) model.HandleButton(GamePadButton.North, GamePadAction.Up);
        }

        public void OnWest(InputAction.CallbackContext c) {
            if (c.started) model.HandleButton(GamePadButton.West, GamePadAction.Down);
            else if (c.canceled) model.HandleButton(GamePadButton.West, GamePadAction.Up);
        }

        public void OnSouth(InputAction.CallbackContext c) {
            if (c.started) model.HandleButton(GamePadButton.South, GamePadAction.Down);
            else if (c.canceled) model.HandleButton(GamePadButton.South, GamePadAction.Up);
        }

        public void OnEast(InputAction.CallbackContext c) {
            if (c.started) model.HandleButton(GamePadButton.East, GamePadAction.Down);
            else if (c.canceled) model.HandleButton(GamePadButton.East, GamePadAction.Up);
        }

        public void OnRightShoulder(InputAction.CallbackContext c) {
            if (c.started) model.HandleButton(GamePadButton.RightShoulder, GamePadAction.Down);
            else if (c.canceled) model.HandleButton(GamePadButton.RightShoulder, GamePadAction.Up);
        }

        public void OnLeftShoulder(InputAction.CallbackContext c) {
            if (c.started) model.HandleButton(GamePadButton.LeftShoulder, GamePadAction.Down);
            else if (c.canceled) model.HandleButton(GamePadButton.LeftShoulder, GamePadAction.Up);
        }

        public void OnRightTrigger(InputAction.CallbackContext c) {
            if (c.started) model.HandleButton(GamePadButton.RightTrigger, GamePadAction.Down);
            else if (c.canceled) model.HandleButton(GamePadButton.RightTrigger, GamePadAction.Up);
        }

        public void OnLeftTrigger(InputAction.CallbackContext c) {
            if (c.started) model.HandleButton(GamePadButton.LeftTrigger, GamePadAction.Down);
            else if (c.canceled) model.HandleButton(GamePadButton.LeftTrigger, GamePadAction.Up);
        }

        public void OnEscape(InputAction.CallbackContext c) {
            if (c.started) model.HandleButton(GamePadButton.Escape, GamePadAction.Down);
            else if (c.canceled) model.HandleButton(GamePadButton.Escape, GamePadAction.Up);
        }
    }
}
