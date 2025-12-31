
using Core.GamePad.Types;
using UnityEngine;

namespace Core.GamePad.Models {

    public interface IGamePadModel {
        /// <summary>ゲームパッドID</summary>
        GamePadId Id { get; }

        /// <summary>
        /// 初期化（共有入力モデルを設定）
        /// </summary>
        void Initialize(IGamePadInputModel sharedModel);

        /// <summary>現在の入力方向を取得</summary>
        Vector2 GetDirection();

        /// <summary>
        /// 入力方向を処理する
        /// </summary>
        void HandleDirection(Vector2 v);

        /// <summary>
        /// ボタン入力を処理する
        /// </summary>
        void HandleButton(GamePadButton button, GamePadAction action);

        void OnEnable();
        void OnDisable();
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
