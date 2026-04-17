using System;
using System.Collections.Generic;
using Alice;
using R3;
using UnityEngine;



public interface IStrikerStateContext : IStrikerContext {
    void TryTransition(IStrikerNode node, bool forceSameStateTransition = false);
    void PreventGroup();
    void ClearPreventGroup();
    bool IsGroupProcessingPrevented { get; }
    new void PlayAnimation(StrikerAnimationClip animation, Vector3 positionOffset, Vector3 rotationOffset, Action<IStrikerStateContext> onComplete = null);
}


public interface IStrikerState {
    StrikerStateCategory Category { get; }
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
    public abstract StrikerStateCategory Category { get; }
    public virtual IEnumerable<IStrikerGroup> Parents => parents;
    protected CompositeDisposable disposables = new();

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
        ctx.ClearPreventGroup();
        // タイムアクション処理
        try {
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

            OnUpdate(ctx);

            if (!ctx.IsGroupProcessingPrevented) {
                foreach (var p in parents) {
                    if (ctx.IsGroupProcessingPrevented) {
                        break;
                    }
                    p.OnUpdate(ctx);
                }
            }
        }
        finally {
            ctx.ClearPreventGroup();
        }
    }

    void IStrikerState.OnHit(IStrikerStateContext ctx, HitStatus status) {
        ctx.ClearPreventGroup();
        try {
            OnHit(ctx, status);

            if (!ctx.IsGroupProcessingPrevented) {
                foreach (var p in parents) {
                    if (ctx.IsGroupProcessingPrevented) {
                        break;
                    }
                    p.OnHit(ctx, status);
                }
            }
        }
        finally {
            ctx.ClearPreventGroup();
        }
    }

    void IStrikerState.OnEnemyBehind(IStrikerStateContext ctx) {
        ctx.ClearPreventGroup();
        try {
            OnEnemyBehind(ctx);

            if (!ctx.IsGroupProcessingPrevented) {
                foreach (var p in parents) {
                    if (ctx.IsGroupProcessingPrevented) {
                        break;
                    }
                    p.OnEnemyBehind(ctx);
                }
            }
        }
        finally {
            ctx.ClearPreventGroup();
        }
    }

    void IStrikerState.OnAttackRequested(IStrikerStateContext ctx) {
        ctx.ClearPreventGroup();
        try {
            OnAttackRequested(ctx);

            if (!ctx.IsGroupProcessingPrevented) {
                foreach (var p in parents) {
                    if (ctx.IsGroupProcessingPrevented) {
                        break;
                    }
                    p.OnAttackRequested(ctx);
                }
            }
        }
        finally {
            ctx.ClearPreventGroup();
        }
    }

    void IStrikerState.OnSpecialRequested(IStrikerStateContext ctx) {
        ctx.ClearPreventGroup();
        try {
            OnSpecialRequested(ctx);

            if (!ctx.IsGroupProcessingPrevented) {
                foreach (var p in parents) {
                    if (ctx.IsGroupProcessingPrevented) {
                        break;
                    }
                    p.OnSpecialRequested(ctx);
                }
            }
        }
        finally {
            ctx.ClearPreventGroup();
        }
    }

    void IStrikerState.OnChargeRequested(IStrikerStateContext ctx) {
        ctx.ClearPreventGroup();
        try {
            OnChargeRequested(ctx);

            if (!ctx.IsGroupProcessingPrevented) {
                foreach (var p in parents) {
                    if (ctx.IsGroupProcessingPrevented) {
                        break;
                    }
                    p.OnChargeRequested(ctx);
                }
            }
        }
        finally {
            ctx.ClearPreventGroup();
        }
    }

    void IStrikerState.OnDashRequested(IStrikerStateContext ctx) {
        ctx.ClearPreventGroup();
        try {
            OnDashRequested(ctx);

            if (!ctx.IsGroupProcessingPrevented) {
                foreach (var p in parents) {
                    if (ctx.IsGroupProcessingPrevented) {
                        break;
                    }
                    p.OnDashRequested(ctx);
                }
            }
        }
        finally {
            ctx.ClearPreventGroup();
        }
    }

    void IStrikerState.OnGuardRequested(IStrikerStateContext ctx) {
        ctx.ClearPreventGroup();
        try {
            OnGuardRequested(ctx);

            if (!ctx.IsGroupProcessingPrevented) {
                foreach (var p in parents) {
                    if (ctx.IsGroupProcessingPrevented) {
                        break;
                    }
                    p.OnGuardRequested(ctx);
                }
            }
        }
        finally {
            ctx.ClearPreventGroup();
        }
    }

    void IStrikerState.OnMiss(IStrikerStateContext ctx) {
        ctx.ClearPreventGroup();
        try {
            OnMiss(ctx);

            if (!ctx.IsGroupProcessingPrevented) {
                foreach (var p in parents) {
                    if (ctx.IsGroupProcessingPrevented) {
                        break;
                    }
                    p.OnMiss(ctx);
                }
            }
        }
        finally {
            ctx.ClearPreventGroup();
        }
    }

    protected void ScheduleStateEvent(float delay, Action<IStrikerStateContext> action) {
        timeActions.Add((delay, 0f, action));
    }

    void IStrikerState.OnEnter(IStrikerContext ctx) {
        disposables.Dispose();
        disposables = new CompositeDisposable();
        OnEnter(ctx);
    }

    void IStrikerState.OnExit(IStrikerContext ctx) {
        timeActions.Clear();
        OnExit(ctx);
        disposables.Dispose();
        disposables = new CompositeDisposable();
    }
}
