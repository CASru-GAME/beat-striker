using System;
using Core.Battle;

namespace Core.Striker
{
    public interface IStrikerState
    {
        void Enter(IStrikerHub hub);
        void Exit();
        void OnUpdate(IStrikerHub hub);

        void OnHit(IStrikerHub hub,HitStatus status);
        void OnAttackRequested(IStrikerHub hub);
        void OnChargeRequested(IStrikerHub hub);
        void OnGuardRequested(IStrikerHub hub);
        void OnDashRequested(IStrikerHub hub);
        void OnMiss(IStrikerHub hub);
    }
}
