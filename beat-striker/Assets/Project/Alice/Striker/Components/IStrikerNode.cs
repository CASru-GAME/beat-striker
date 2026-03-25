using System;
using System.Collections.Generic;
using Alice;
using UnityEngine;

namespace Alice {
    public interface IStrikerContext {
        Rigidbody Rigidbody { get; }
        Vector2 InputDirection { get; }
        IEnumerable<IReadOnlyBattleEntity> GetAllStrikers();
        void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null);
        void ApplyDamage(float damage);
    }

    public interface IStrikerStateContext {
        void TryTransition(IStrikerNode node);
        Rigidbody Rigidbody { get; }
        Vector2 InputDirection { get; }
        IEnumerable<IReadOnlyBattleEntity> GetAllStrikers();
        void ApplyDamage(float damage);
        void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null);
    }

    public interface IStrikerNodeContext : IStrikerStateContext {
        void ChangeState(IStrikerState state);
    }

    public interface IStrikerNode {
        void OnTryTransition(IStrikerNodeContext context);
    }

    public interface IStrikerState {
        IEnumerable<IStrikerGroup> Parents { get; }
        void OnEnter(IStrikerContext context);
        void OnExit(IStrikerContext context);
        void OnUpdate(IStrikerStateContext context);
        void OnHit(IStrikerStateContext context, HitStatus status);
        void OnAttackRequested(IStrikerStateContext context);
        void OnChargeRequested(IStrikerStateContext context);
        void OnGuardRequested(IStrikerStateContext context);
        void OnDashRequested(IStrikerStateContext context);
        void OnMiss(IStrikerStateContext context);
    }

    public interface IStrikerGroup {
        void OnEnter(IStrikerContext context);
        void OnExit(IStrikerContext context);
        void OnUpdate(IStrikerStateContext context);
        void OnHit(IStrikerStateContext context, HitStatus status);
        void OnAttackRequested(IStrikerStateContext context);
        void OnChargeRequested(IStrikerStateContext context);
        void OnGuardRequested(IStrikerStateContext context);
        void OnDashRequested(IStrikerStateContext context);
        void OnMiss(IStrikerStateContext context);
    }
}