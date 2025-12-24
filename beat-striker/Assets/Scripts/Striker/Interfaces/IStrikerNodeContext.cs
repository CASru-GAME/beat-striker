using Core.App.Types;
using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    public interface IStrikerNodeContext: IStrikerStateContext {
        void ChangeState(IStrikerState state);
        void ChangeState(); // デフォルト状態に戻る
    }
}
