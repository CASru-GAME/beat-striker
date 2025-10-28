
using Core.EventBus;
using Core.GamePad.Models;
using Core.GamePad.Presenters;
using Core.GamePad.Types;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Core.GamePad.Views {

    public sealed class GamePad : MonoBehaviour, GameInput.IPlayerActions {
        private PlayerInput playerInput;
        private GameInput input;
        private IGamePadPresenter presenter;

        [Inject]
        public void Construct(IGamePadPresenter presenter, PlayerInput playerInput) {
            this.presenter = presenter;
            this.playerInput = playerInput;
        }

        void Awake() {
            input = new GameInput();
        }

        void OnEnable() {
            input.asset.devices = playerInput.devices;
            input.Player.AddCallbacks(this);
            input.Player.Enable();
            presenter.OnEnable();
        }

        void OnDisable() {
            input.Player.RemoveCallbacks(this);
            input.Player.Disable();
            presenter.OnDisable();
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
