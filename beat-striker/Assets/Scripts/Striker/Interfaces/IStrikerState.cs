using System;
using Core.Battle;

namespace Core.Striker
{
    public interface IStrikerState
    {
        void OnEnter(IStrikerContext hub);
        void OnExit(IStrikerContext hub);
        void OnUpdate(IStrikerStateContext hub);

        void OnHit(IStrikerStateContext hub, HitStatus status);
        void OnAttackRequested(IStrikerStateContext hub);
        void OnChargeRequested(IStrikerStateContext hub);
        void OnGuardRequested(IStrikerStateContext hub);
        void OnDashRequested(IStrikerStateContext hub);
        void OnMiss(IStrikerStateContext hub);
    }
}
