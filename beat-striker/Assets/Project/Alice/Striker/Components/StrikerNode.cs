using UnityEngine;

namespace Alice {
    public abstract class StrikerNode : MonoBehaviour, IStrikerNode {
        
        public abstract void OnTryTransition(IStrikerNodeContext context);
    }
}