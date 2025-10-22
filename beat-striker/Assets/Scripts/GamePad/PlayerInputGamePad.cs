
using UnityEngine;
using UnityEngine.InputSystem;
using Core.EventBus;

namespace Core.GamePad {

    /// <summary>
    /// GamePad等操作からゲームパッドのコマンドを発行するコンポーネント
    /// GamePad等が接続されたときにアタッチされる
    /// GamePad等の入力に応じてゲームパッドのコマンドメッセージを発行する
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputGamePad : MonoBehaviour, GameInput.IPlayerActions {
        private static int nextHumanId = 1024;
        private PlayerInput playerInput;
        private GameInput input;
        private bool directionDown;
        [SerializeField] private float DIR_ON_THRESHOLD = 0.2f;
        [SerializeField] private float DIR_OFF_THRESHOLD = 0.15f;
        private GamePadId humanId;

        void Awake() {
            humanId = new GamePadId(nextHumanId++);
            playerInput = GetComponent<PlayerInput>();
            input = new GameInput();
        }

        void OnEnable() {
            input.asset.devices = playerInput.devices;
            input.Player.AddCallbacks(this);
            input.Player.Enable();
            directionDown = false;
            Bus.Publish(new GamePadJoinedMessage(humanId));
        }

        void OnDisable() {
            input.Player.RemoveCallbacks(this);
            input.Player.Disable();
            Bus.Publish(new GamePadLeftMessage(humanId));
        }

        void OnDestroy() {
            input.Dispose();
        }

        public void OnDirection(InputAction.CallbackContext context) {
            var val = context.ReadValue<Vector2>();
            float mag = val.magnitude;

            bool nextDown = directionDown ? (mag >= DIR_OFF_THRESHOLD)
                                          : (mag >= DIR_ON_THRESHOLD);

            var direction = nextDown ? val.normalized : Vector2.zero;

            Bus.Publish(new GamePadDirectionMessage(humanId,direction));

            if (nextDown != directionDown) {
                directionDown = nextDown;
                Bus.Publish(new GamePadMessage(humanId,GamePadButton.Direction,
                    directionDown ? GamePadAction.Down : GamePadAction.Up));
            }
        }

        public void OnNorth(InputAction.CallbackContext context) {
            if (context.started) Bus.Publish(new GamePadMessage(humanId, GamePadButton.North, GamePadAction.Down));
            else if (context.canceled) Bus.Publish(new GamePadMessage(humanId, GamePadButton.North, GamePadAction.Up));
        }
        public void OnWest(InputAction.CallbackContext context) {
            if (context.started) Bus.Publish(new GamePadMessage(humanId, GamePadButton.West, GamePadAction.Down));
            else if (context.canceled) Bus.Publish(new GamePadMessage(humanId, GamePadButton.West, GamePadAction.Up));
        }
        public void OnSouth(InputAction.CallbackContext context) {
            if (context.started) Bus.Publish(new GamePadMessage(humanId, GamePadButton.South, GamePadAction.Down));
            else if (context.canceled) Bus.Publish(new GamePadMessage(humanId, GamePadButton.South, GamePadAction.Up));
        }
        public void OnEast(InputAction.CallbackContext context) {
            if (context.started) Bus.Publish(new GamePadMessage(humanId, GamePadButton.East, GamePadAction.Down));
            else if (context.canceled) Bus.Publish(new GamePadMessage(humanId, GamePadButton.East, GamePadAction.Up));
        }

        public void OnRightShoulder(InputAction.CallbackContext context) {
            if (context.started) Bus.Publish(new GamePadMessage(humanId, GamePadButton.RightShoulder, GamePadAction.Down));
            else if (context.canceled) Bus.Publish(new GamePadMessage(humanId, GamePadButton.RightShoulder, GamePadAction.Up));
        }

        public void OnLeftShoulder(InputAction.CallbackContext context) {
            if (context.started) Bus.Publish(new GamePadMessage(humanId, GamePadButton.LeftShoulder, GamePadAction.Down));
            else if (context.canceled) Bus.Publish(new GamePadMessage(humanId, GamePadButton.LeftShoulder, GamePadAction.Up));
        }

        public void OnRightTrigger(InputAction.CallbackContext context) {
            if (context.started) Bus.Publish(new GamePadMessage(humanId, GamePadButton.RightTrigger, GamePadAction.Down));
            else if (context.canceled) Bus.Publish(new GamePadMessage(humanId, GamePadButton.RightTrigger, GamePadAction.Up));
        }

        public void OnLeftTrigger(InputAction.CallbackContext context) {
            if (context.started) Bus.Publish(new GamePadMessage(humanId, GamePadButton.LeftTrigger, GamePadAction.Down));
            else if (context.canceled) Bus.Publish(new GamePadMessage(humanId, GamePadButton.LeftTrigger, GamePadAction.Up));
        }

        public void OnEscape(InputAction.CallbackContext context) {
            if (context.started) Bus.Publish(new GamePadMessage(humanId, GamePadButton.Escape, GamePadAction.Down));
            else if (context.canceled) Bus.Publish(new GamePadMessage(humanId, GamePadButton.Escape, GamePadAction.Up));
        }

    }

}