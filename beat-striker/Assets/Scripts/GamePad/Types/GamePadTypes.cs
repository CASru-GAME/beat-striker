
using System;

namespace Core.GamePad.Types {

    /// <summary>
    /// ゲームパッドID
    /// 主にコントローラやキーボードなどの入力デバイスを識別するために使用される
    /// </summary>
    [Serializable]
    public struct GamePadId {
        public int value;
        public GamePadId(int value) {
            this.value = value;
        }
    }

    /// <summary>
    /// ゲームパッドのボタン種類
    /// </summary>
    public enum GamePadButton {
        North,
        West,
        South,
        East,
        Direction,
        RightShoulder,
        LeftShoulder,
        RightTrigger,
        LeftTrigger,
        Escape
    }

    /// <summary>
    /// ゲームパッドのアクションで押されたか離されたか
    /// </summary>
    public enum GamePadAction {
        Up, Down
    }
}