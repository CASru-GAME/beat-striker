using System;
using System.Collections.Generic;
using Alice;
using UnityEngine;


public record StrikerInpact(Vector3 DirectionAndMagnitude);
public record AttentionRequest(float DurationSeconds);

public interface IStrikerContext {
    Rigidbody Rigidbody { get; }
    Vector2 InputDirection { get; }
    IEnumerable<IReadOnlyBattleEntity> GetAllStrikers();
    IReadOnlyBattleEntity GetSelf();
    IReadOnlyBattleEntity GetOpponent();
    void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null);
    void ApplyDamage(float damage);
    void GenerateInpact(StrikerInpact command);
    void RequestAttention(AttentionRequest request);
}

public interface IStrikerStateContext : IStrikerContext {
    void TryTransition(IStrikerNode node, bool forceSameStateTransition = false);
}

public interface IStrikerNodeContext : IStrikerStateContext {
    void ChangeState(IStrikerState state, bool forceSameStateTransition = false);
}

public interface IStrikerNode {
    void OnTryTransition(IStrikerNodeContext context);
}

public interface IStrikerState {
    IEnumerable<IStrikerGroup> Parents { get; }
    void OnEnter(IStrikerContext context);
    void OnExit(IStrikerContext context);
    void OnUpdate(IStrikerStateContext context);
    void OnEnemyBehind(IStrikerStateContext context);
    void OnHit(IStrikerStateContext context, HitStatus status);
    void OnAttackRequested(IStrikerStateContext context);
    void OnSpecialRequested(IStrikerStateContext context);
    void OnChargeRequested(IStrikerStateContext context);
    void OnGuardRequested(IStrikerStateContext context);
    void OnDashRequested(IStrikerStateContext context);
    void OnMiss(IStrikerStateContext context);
}

public interface IStrikerGroup {
    void OnEnter(IStrikerContext context);
    void OnExit(IStrikerContext context);
    void OnUpdate(IStrikerStateContext context);
    void OnEnemyBehind(IStrikerStateContext context);
    void OnHit(IStrikerStateContext context, HitStatus status);
    void OnAttackRequested(IStrikerStateContext context);
    void OnSpecialRequested(IStrikerStateContext context);
    void OnChargeRequested(IStrikerStateContext context);
    void OnGuardRequested(IStrikerStateContext context);
    void OnDashRequested(IStrikerStateContext context);
    void OnMiss(IStrikerStateContext context);
}