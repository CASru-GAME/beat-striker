using System;

namespace Core.Striker
{
    public interface IStrikerState
    {
        void Enter();
        void Exit();
        void OnUpdate();
        bool TryTransition(IStrikerState targetState, IStrikerTransitionRequest request);
    }
}
