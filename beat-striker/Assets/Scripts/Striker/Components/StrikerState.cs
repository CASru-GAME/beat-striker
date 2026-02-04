using System;
using System.Collections.Generic;
using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    public abstract class StrikerState : StrikerNode, IStrikerState {
        [SerializeField] private List<StrikerGroup> parents = new List<StrikerGroup>();
        public virtual IEnumerable<IGroup<IStrikerContext>> Parents => parents;

        private readonly List<(float delay, float elapsedTime, Action<IStrikerStateContext> action)> timeActions = new();

        public sealed override void OnTryTransition(IStrikerNodeContext context) {
            context.ChangeState(this);
        }

        public virtual void OnEnter(IStrikerContext hub) { }
        public virtual void OnUpdate(IStrikerStateContext hub) { }
        public virtual void OnExit(IStrikerContext hub) { }
        public virtual void OnAttackRequested(IStrikerStateContext hub) { }
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
                    action?.Invoke(ctx);
                    timeActions.RemoveAt(i);
                } else {
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

        void IStrikerState.OnAttackRequested(IStrikerStateContext ctx) {
            foreach (var p in parents) {
                p.OnAttackRequested(ctx);
            }
            OnAttackRequested(ctx);
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
}
