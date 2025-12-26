using Core.App.Types;
using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    public interface IStrikerStateContext : Core.Battle.IStrikerView, IStrikerContext {
        void TryTransition(IStrikerNode node);
        void TryTransition();
    }
}
