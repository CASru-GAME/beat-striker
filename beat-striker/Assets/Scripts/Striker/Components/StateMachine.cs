


using System;
using System.Collections.Generic;
using Core.Battle;
using UnityEngine;

namespace Core.Striker {

    /// <summary>
    /// 汎用ステートマシン (CRTP: Curiously Recurring Template Pattern)
    /// TSelf: 派生クラス自身の型。これによりTryTransitionで正しい型をNodeに渡せる
    /// </summary>
    public abstract class StateMachine<TNode, TState, TContext, TSelf> : INodeContext<TNode, TState>
        where TNode : INode<TSelf>
        where TState : IState<TContext, TState>
        where TSelf : StateMachine<TNode, TState, TContext, TSelf>
    {
        private TState currentState;
        protected readonly TContext context;

        public TState CurrentState => currentState;

        protected StateMachine(TContext context, TState defaultState = default) {
            this.context = context;
            if (defaultState != null) ChangeState(defaultState);
        }

        public void ChangeState(TState newState) {
            if (newState == null || ReferenceEquals(newState, currentState)) return;

            var oldParents = currentState != null
                ? new HashSet<IGroup<TContext>>(currentState.Parents ?? Array.Empty<IGroup<TContext>>())
                : new HashSet<IGroup<TContext>>();
            var newParents = new HashSet<IGroup<TContext>>(newState.Parents ?? Array.Empty<IGroup<TContext>>());

            // 現ステートを先にExit
            currentState?.OnExit(context);

            // 旧にあって新にない親をExit
            foreach (var parent in oldParents) {
                if (!newParents.Contains(parent)) parent.OnExit(context);
            }

            // 新にあって旧にない親をEnter
            foreach (var parent in newParents) {
                if (!oldParents.Contains(parent)) parent.OnEnter(context);
            }

            // 新しいステートをEnter
            newState.OnEnter(context);

            currentState = newState;
        }

        public void TryTransition(TNode node) {
            node?.OnTryTransition((TSelf)this);
        }

        public void Reset(TState defaultState) {
            ChangeState(defaultState);
        }
    }
}