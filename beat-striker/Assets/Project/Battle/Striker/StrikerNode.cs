using UnityEngine;

public interface IStrikerNodeContext : IStrikerStateContext {
    void ChangeState(IStrikerState state, bool forceSameStateTransition = false);
}

public interface IStrikerNode {
    void OnTryTransition(IStrikerNodeContext context);
}

public abstract class StrikerNode : MonoBehaviour, IStrikerNode {

    public abstract void OnTryTransition(IStrikerNodeContext context);
}
