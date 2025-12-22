using UnityEngine;

namespace Core.Striker {
    public abstract class StrikerState : MonoBehaviour, IStrikerState {
        public virtual void Enter(StrikerStateContext context) { }
        public virtual void Exit() { }
        public virtual void OnUpdate(StrikerStateContext context) { }

        public virtual bool TryTransition(IStrikerState targetState, IStrikerTransitionRequest request) {
            return true;
        }
    }
}
