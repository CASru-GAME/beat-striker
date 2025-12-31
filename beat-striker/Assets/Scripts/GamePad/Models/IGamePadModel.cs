
using Core.GamePad.Types;
using UnityEngine;

namespace Core.GamePad.Models {

    public interface IGamePadModel {
        /// <summary>ゲームパッドID</summary>
        GamePadId Id { get; }

        /// <summary>現在の入力方向を取得</summary>
        Vector2 GetDirection();

        /// <summary>
        /// 入力方向を適用し、状態変化を返す
        /// </summary>
        /// <param name="v">入力ベクトル</param>
        DirectionResult ApplyDirection(Vector2 v);
    }


    /// <summary>
    /// 方向入力の適用結果
    /// </summary>
    public readonly struct DirectionResult {
        /// <summary>状態が変化したか</summary>
        public readonly bool downStateChanged;
        /// <summary>現在押下状態</summary>
        public readonly bool downState;

        public DirectionResult(bool downStateChanged, bool downState) {
            this.downStateChanged = downStateChanged;
            this.downState = downState;
        }
    }

}
