
using UnityEngine;

namespace Core.GamePad.Types {
    public static class GamePadMessages {
        /// <summary>
        /// ゲームパッドのコマンド入力を示すメッセージ
        /// </summary>
        public class Inputed {
            public readonly GamePadId gamePadId;
            public readonly GamePadButton button;
            public readonly GamePadAction action;

            public Inputed(GamePadId id, GamePadButton button, GamePadAction action) {
                this.gamePadId = id;
                this.button = button;
                this.action = action;
            }
        }

        /// <summary>
        /// ゲームパッドの方向入力を示すメッセージ
        /// </summary>
        public class DirectionChanged {
            public readonly GamePadId gamePadId;
            public readonly Vector2 direction;

            public DirectionChanged(GamePadId id, Vector2 direction) {
                this.gamePadId = id;
                this.direction = direction;
            }
        }

        /// <summary>
        /// ゲームパッドが参加したことを示すメッセージ
        /// </summary>
        public class Joined {
            public readonly GamePadId gamePadId;

            public Joined(GamePadId id) {
                this.gamePadId = id;
            }
        }

        /// <summary>
        /// ゲームパッドが離脱したことを示すメッセージ
        /// </summary>
        public class Left {
            public readonly GamePadId gamePadId;

            public Left(GamePadId id) {
                this.gamePadId = id;
            }
        }
    }
}
