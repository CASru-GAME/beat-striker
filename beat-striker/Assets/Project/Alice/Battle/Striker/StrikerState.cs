using System;
using System.Collections.Generic;
using Alice;
using UnityEngine;



public interface IStrikerStateContext : IStrikerContext {
    void TryTransition(IStrikerNode node, bool forceSameStateTransition = false);
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


public abstract class StrikerState : StrikerNode, IStrikerState {
    [SerializeField] private List<StrikerGroup> parents = new List<StrikerGroup>();
    public virtual IEnumerable<IStrikerGroup> Parents => parents;

    private readonly List<(float delay, float elapsedTime, Action<IStrikerStateContext> action)> timeActions = new();

    public sealed override void OnTryTransition(IStrikerNodeContext context) {
        context.ChangeState(this);
    }

    public virtual void OnEnter(IStrikerContext hub) { }
    public virtual void OnUpdate(IStrikerStateContext hub) { }
    public virtual void OnExit(IStrikerContext hub) { }
    public virtual void OnEnemyBehind(IStrikerStateContext hub) { }
    public virtual void OnAttackRequested(IStrikerStateContext hub) { }
    public virtual void OnSpecialRequested(IStrikerStateContext hub) { }
    public virtual void OnChargeRequested(IStrikerStateContext hub) { }
    public virtual void OnDashRequested(IStrikerStateContext hub) { }
    public virtual void OnGuardRequested(IStrikerStateContext hub) { }
    public virtual void OnHit(IStrikerStateContext hub, HitStatus status) { }
    public virtual void OnMiss(IStrikerStateContext hub) { }

    void IStrikerState.OnUpdate(IStrikerStateContext ctx) {
        // タイムアクション処理
        for (int i = timeActions.Count - 1; i >= 0; i--) {
            var (delay, elapsedTime, action) = timeActions[i];
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= delay) {
                timeActions.RemoveAt(i);
                action?.Invoke(ctx);
            }
            else {
                timeActions[i] = (delay, elapsedTime, action);
            }
        }

        foreach (var p in parents) {
            p.OnUpdate(ctx);
        }
        OnUpdate(ctx);
    }

    void IStrikerState.OnHit(IStrikerStateContext ctx, HitStatus status) {
        foreach (var p in parents) {
            p.OnHit(ctx, status);
        }
        OnHit(ctx, status);
    }

    void IStrikerState.OnEnemyBehind(IStrikerStateContext ctx) {
        foreach (var p in parents) {
            p.OnEnemyBehind(ctx);
        }
        OnEnemyBehind(ctx);
    }

    void IStrikerState.OnAttackRequested(IStrikerStateContext ctx) {
        foreach (var p in parents) {
            p.OnAttackRequested(ctx);
        }
        OnAttackRequested(ctx);
    }

    void IStrikerState.OnSpecialRequested(IStrikerStateContext ctx) {
        foreach (var p in parents) {
            p.OnSpecialRequested(ctx);
        }
        OnSpecialRequested(ctx);
    }

    void IStrikerState.OnChargeRequested(IStrikerStateContext ctx) {
        foreach (var p in parents) {
            p.OnChargeRequested(ctx);
        }
        OnChargeRequested(ctx);
    }

    void IStrikerState.OnDashRequested(IStrikerStateContext ctx) {
        foreach (var p in parents) {
            p.OnDashRequested(ctx);
        }
        OnDashRequested(ctx);
    }

    void IStrikerState.OnGuardRequested(IStrikerStateContext ctx) {
        foreach (var p in parents) {
            p.OnGuardRequested(ctx);
        }
        OnGuardRequested(ctx);
    }

    void IStrikerState.OnMiss(IStrikerStateContext ctx) {
        foreach (var p in parents) {
            p.OnMiss(ctx);
        }
        OnMiss(ctx);
    }

    protected void ScheduleStateEvent(float delay, Action<IStrikerStateContext> action) {
        timeActions.Add((delay, 0f, action));
    }

    void IStrikerState.OnExit(IStrikerContext ctx) {
        timeActions.Clear();
        OnExit(ctx);
    }
}
