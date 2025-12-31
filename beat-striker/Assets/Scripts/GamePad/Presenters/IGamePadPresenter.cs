
using Core.GamePad.Types;
using UnityEngine;

namespace Core.GamePad.Presenters {

    public interface IGamePadPresenter {

        /// <summary>
        /// 方向入力を処理
        /// </summary>
        /// <param name="v">入力ベクトル</param>
        void OnDirection(Vector2 v);

        /// <summary>
        /// ボタン入力を処理
        /// </summary>
        /// <param name="button">入力ボタン</param>
        /// <param name="isDown">押下状態</param>
        void OnButton(GamePadButton button, GamePadAction action);
    }
}
