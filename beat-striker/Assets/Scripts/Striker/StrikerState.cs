using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    public abstract class StrikerState : MonoBehaviour, IStrikerState {
        public virtual void Enter(IStrikerHub hub) { }
        public virtual void Exit() { }

        public void OnAttackRequested(IStrikerHub hub) {
        }

        public void OnChargeRequested(IStrikerHub hub) {
        }

        public void OnDashRequested(IStrikerHub hub) {
        }

        public void OnGuardRequested(IStrikerHub hub) {
        }

        public void OnHit(IStrikerHub hub, HitStatus status) {
        }

        public void OnMiss(IStrikerHub hub) {
        }

        public virtual void OnUpdate(IStrikerHub hub) { 

        }
    }
}
