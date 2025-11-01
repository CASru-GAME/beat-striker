

using Core.GamePad.Models;
using Core.GamePad.Presenters;
using Core.GamePad.Types;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.GamePad.Views {
    [RequireComponent(typeof(PlayerInput))]
    public sealed class GamePadView : MonoBehaviour, GameInput.IPlayerActions {
        private PlayerInput playerInput;
        private GameInput input;
        private IGamePadPresenter presenter;
        private ILifeMutater lifeMutater;

        public void Construct(IGamePadPresenter presenter, ILifeMutater lifeMutater) {
            this.presenter = presenter;
            this.lifeMutater = lifeMutater;
        }

        void Awake() {
            input = new GameInput();
            playerInput = GetComponent<PlayerInput>();
        }

        void OnEnable() {
            input.asset.devices = playerInput.devices;
            input.Player.AddCallbacks(this);
            input.Player.Enable();
            playerInput.onControlsChanged += OnControlsChanged;
            var devCount = playerInput.devices.Count;
            Debug.Log($"GamePadView OnEnable: deviceCount={devCount} presenterSet={(presenter!=null)} lifeMutater={(lifeMutater!=null)}");
            lifeMutater?.SetEnable(true);
        }

        void OnDisable() {
            var devCount2 = playerInput.devices.Count;
            Debug.Log($"GamePadView OnDisable: deviceCount={devCount2} presenterSet={(presenter!=null)} lifeMutater={(lifeMutater!=null)}");
            input.Player.RemoveCallbacks(this);
            input.Player.Disable();
            playerInput.onControlsChanged -= OnControlsChanged;
            lifeMutater?.SetEnable(false);
        }

        private void OnControlsChanged(PlayerInput changed) {
            if (changed == playerInput)
                input.asset.devices = playerInput.devices;
        }

        void OnDestroy() => input.Dispose();

        public void OnDirection(InputAction.CallbackContext c)
            => presenter.OnDirection(c.ReadValue<Vector2>());

        public void OnNorth(InputAction.CallbackContext c) {
            if (c.started) presenter.OnButton(GamePadButton.North, GamePadAction.Down);
            else if (c.canceled) presenter.OnButton(GamePadButton.North, GamePadAction.Up);
        }

        public void OnWest(InputAction.CallbackContext c) {
            if (c.started) presenter.OnButton(GamePadButton.West, GamePadAction.Down);
            else if (c.canceled) presenter.OnButton(GamePadButton.West, GamePadAction.Up);
        }

        public void OnSouth(InputAction.CallbackContext c) {
            if (c.started) presenter.OnButton(GamePadButton.South, GamePadAction.Down);
            else if (c.canceled) presenter.OnButton(GamePadButton.South, GamePadAction.Up);
        }

        public void OnEast(InputAction.CallbackContext c) {
            if (c.started) presenter.OnButton(GamePadButton.East, GamePadAction.Down);
            else if (c.canceled) presenter.OnButton(GamePadButton.East, GamePadAction.Up);
        }

        public void OnRightShoulder(InputAction.CallbackContext c) {
            if (c.started) presenter.OnButton(GamePadButton.RightShoulder, GamePadAction.Down);
            else if (c.canceled) presenter.OnButton(GamePadButton.RightShoulder, GamePadAction.Up);
        }

        public void OnLeftShoulder(InputAction.CallbackContext c) {
            if (c.started) presenter.OnButton(GamePadButton.LeftShoulder, GamePadAction.Down);
            else if (c.canceled) presenter.OnButton(GamePadButton.LeftShoulder, GamePadAction.Up);
        }

        public void OnRightTrigger(InputAction.CallbackContext c) {
            if (c.started) presenter.OnButton(GamePadButton.RightTrigger, GamePadAction.Down);
            else if (c.canceled) presenter.OnButton(GamePadButton.RightTrigger, GamePadAction.Up);
        }

        public void OnLeftTrigger(InputAction.CallbackContext c) {
            if (c.started) presenter.OnButton(GamePadButton.LeftTrigger, GamePadAction.Down);
            else if (c.canceled) presenter.OnButton(GamePadButton.LeftTrigger, GamePadAction.Up);
        }

        public void OnEscape(InputAction.CallbackContext c) {
            if (c.started) presenter.OnButton(GamePadButton.Escape, GamePadAction.Down);
            else if (c.canceled) presenter.OnButton(GamePadButton.Escape, GamePadAction.Up);
        }
    }
}
