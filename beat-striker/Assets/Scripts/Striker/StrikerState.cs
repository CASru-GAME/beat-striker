using UnityEngine;

namespace Core.Striker {
    public abstract class StrikerState : MonoBehaviour, IStrikerState {
        protected IStrikerHub hub;
        protected Rigidbody rb;
        protected Animator anim;

        public virtual void Setup(IStrikerHub hub, Rigidbody rb, Animator anim) {
            this.hub = hub;
            this.rb = rb;
            this.anim = anim;
        }

        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void OnUpdate() { }

        public virtual bool TryTransition(IStrikerState targetState, IStrikerTransitionRequest request) {
            return true;
        }
    }
}
