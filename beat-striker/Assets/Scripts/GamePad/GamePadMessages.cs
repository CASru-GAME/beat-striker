
using UnityEngine;

namespace Core.GamePad {
    /// <summary>
    /// ゲームパッドのコマンド入力を示すメッセージ
    /// </summary>
    public class GamePadMessage {
        public readonly GamePadId humanId;
        public readonly GamePadButton button;
        public readonly GamePadAction humanAction;

        public GamePadMessage(GamePadId id, GamePadButton button, GamePadAction humanAction) {
            this.humanId = id;
            this.button = button;
            this.humanAction = humanAction;
        }
    }

    /// <summary>
    /// ゲームパッドの方向入力を示すメッセージ
    /// </summary>
    public class GamePadDirectionMessage {
        public readonly GamePadId humanId;
        public readonly Vector2 direction;

        public GamePadDirectionMessage(GamePadId id, Vector2 direction) {
            this.humanId = id;
            this.direction = direction;
        }
    }

    /// <summary>
    /// ゲームパッドが参加したことを示すメッセージ
    /// </summary>
    public class GamePadJoinedMessage {
        public readonly GamePadId gamePadId;

        public GamePadJoinedMessage(GamePadId id) {
            this.gamePadId = id;
        }
    }

    /// <summary>
    /// ゲームパッドが離脱したことを示すメッセージ
    /// </summary>
    public class GamePadLeftMessage {
        public readonly GamePadId gamePadId;

        public GamePadLeftMessage(GamePadId id) {
            this.gamePadId = id;
        }
    }
}
