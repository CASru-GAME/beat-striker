using UnityEngine;

namespace Core.Striker {
    public abstract class StrikerNode : MonoBehaviour, IStrikerNode {
        
        public abstract void OnTryTransition(IStrikerNodeContext context);
    }
}