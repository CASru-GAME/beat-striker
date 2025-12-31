
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

        public override readonly bool Equals(object obj) {
            if (obj is GamePadId other) {
                return value == other.value;
            }
            return false;
        }

        public override readonly int GetHashCode() {
            return value.GetHashCode();
        }

        public static bool operator ==(GamePadId left, GamePadId right) {
            return left.value == right.value;
        }

        public static bool operator !=(GamePadId left, GamePadId right) {
            return left.value != right.value;
        }

        public override readonly string ToString() {
            return $"GamePadId({value})";
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

    /// <summary>
    /// ゲームパッド入力データ
    /// </summary>
    public readonly struct GamePadInput {
        public readonly GamePadId gamePadId;
        public readonly GamePadButton button;
        public readonly GamePadAction action;
        
        // Properties for case-consistent access
        public GamePadId GamePadId => gamePadId;
        public GamePadButton Button => button;
        public GamePadAction Action => action;
        
        public GamePadInput(GamePadId gamePadId, GamePadButton button, GamePadAction action) {
            this.gamePadId = gamePadId;
            this.button = button;
            this.action = action;
        }
    }
    
    /// <summary>
    /// 方向変更データ
    /// </summary>
    public readonly struct DirectionChange {
        public readonly GamePadId gamePadId;
        public readonly UnityEngine.Vector2 direction;
        
        // Properties for case-consistent access
        public GamePadId GamePadId => gamePadId;
        public UnityEngine.Vector2 Direction => direction;
        
        public DirectionChange(GamePadId gamePadId, UnityEngine.Vector2 direction) {
            this.gamePadId = gamePadId;
            this.direction = direction;
        }
    }
}