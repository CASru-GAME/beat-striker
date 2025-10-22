

using Core.GamePad;

namespace Core.App {

    /// <summary>
    /// プレイヤーとゲームパッドの紐づけが行われたことを示すメッセージ
    /// </summary>
    public class PlayerGamePadBindMessage {
        public readonly PlayerId playerId;
        public readonly GamePadId gamePadId;

        public PlayerGamePadBindMessage(PlayerId playerId, GamePadId gamePadId) {
            this.playerId = playerId;
            this.gamePadId = gamePadId;
        }
    }

    /// <summary>
    /// プレイヤーとゲームパッドの紐づけが解除されたことを示すメッセージ
    /// </summary>
    public class PlayerGamePadUnbindMessage {
        public readonly PlayerId playerId;

        public PlayerGamePadUnbindMessage(PlayerId playerId) {
            this.playerId = playerId;
        }
    }

    public class CursorActivatedMessage {
        public readonly PlayerId playerId;
        public readonly GamePadId gamePadId;

        public CursorActivatedMessage(PlayerId playerId, GamePadId gamePadId) {
            this.playerId = playerId;
            this.gamePadId = gamePadId;
        }
    }
    
    public class CursorDeactivatedMessage {
        public readonly PlayerId playerId;

        public CursorDeactivatedMessage(PlayerId playerId) {
            this.playerId = playerId;
        }
    }
}