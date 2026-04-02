using Alice;
using UnityEngine;



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

public abstract class StrikerGroup : MonoBehaviour, IStrikerGroup {
    public virtual void OnEnter(IStrikerContext hub) { }
    public virtual void OnUpdate(IStrikerStateContext hub) { }
    public virtual void OnEnemyBehind(IStrikerStateContext hub) { }
    public virtual void OnExit(IStrikerContext hub) { }
    public virtual void OnAttackRequested(IStrikerStateContext hub) { }
    public virtual void OnSpecialRequested(IStrikerStateContext hub) { }
    public virtual void OnChargeRequested(IStrikerStateContext hub) { }
    public virtual void OnDashRequested(IStrikerStateContext hub) { }
    public virtual void OnGuardRequested(IStrikerStateContext hub) { }
    public virtual void OnHit(IStrikerStateContext hub, HitStatus status) { }
    public virtual void OnMiss(IStrikerStateContext hub) { }
}

