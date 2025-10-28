
using UnityEngine;

namespace Core.GamePad.Types {
    /// <summary>
    /// ゲームパッドのコマンド入力を示すメッセージ
    /// </summary>
    public class GamePadMessage {
        public readonly GamePadId gamePadId;
        public readonly GamePadButton button;
        public readonly GamePadAction action;

        public GamePadMessage(GamePadId id, GamePadButton button, GamePadAction action) {
            this.gamePadId = id;
            this.button = button;
            this.action = action;
        }
    }

    /// <summary>
    /// ゲームパッドの方向入力を示すメッセージ
    /// </summary>
    public class GamePadDirectionMessage {
        public readonly GamePadId gamePadId;
        public readonly Vector2 direction;

        public GamePadDirectionMessage(GamePadId id, Vector2 direction) {
            this.gamePadId = id;
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
