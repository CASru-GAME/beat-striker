using System;
using Core.Battle;

namespace Core.Striker
{
    public interface IStrikerState
    {
        void Enter(StrikerStateContext context);
        void Exit();
        void OnUpdate(StrikerStateContext context);

        void OnHit(StrikerStateContext context,HitStatus status);
        void OnAttackRequested(StrikerStateContext context);
        void OnChargeRequested(StrikerStateContext context);
        void OnGuardRequested(StrikerStateContext context);
        void OnDashRequested(StrikerStateContext context);
        void OnMiss(StrikerStateContext context);
    }
}
