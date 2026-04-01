using UnityEngine;


public abstract class StrikerNode : MonoBehaviour, IStrikerNode {

    public abstract void OnTryTransition(IStrikerNodeContext context);
}
