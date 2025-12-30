


using System;
using Core.Battle;
using UnityEngine;

namespace Core.Striker {

    /// <summary>
    /// 汎用ステートマシン (CRTP: Curiously Recurring Template Pattern)
    /// TSelf: 派生クラス自身の型。これによりTryTransitionで正しい型をNodeに渡せる
    /// </summary>
    public abstract class StateMachine<TNode, TState, TContext, TSelf> : INodeContext<TNode, TState>
        where TNode : INode<TSelf>
        where TState : IState<TContext>
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
            currentState?.OnExit(context);
            currentState = newState;
            currentState.OnEnter(context);
        }

        public void TryTransition(TNode node) {
            node?.OnTryTransition((TSelf)this);
        }

        public void Reset(TState defaultState) {
            ChangeState(defaultState);
        }
    }

    /// <summary>
    /// Striker専用ステートマシン
    /// 汎用StateMachineを継承し、IStrikerStateContext/IStrikerNodeContextの追加プロパティを実装
    /// </summary>
    public class StrikerStateMachine : 
        StateMachine<IStrikerNode, IStrikerState, IStrikerContext, StrikerStateMachine>,
        IStrikerStateContext, IStrikerNodeContext
    {
        public Rigidbody Rigidbody => context.Rigidbody;
        public Vector2 InputDirection => context.InputDirection;

        public StrikerStateMachine(IStrikerContext context, IStrikerState defaultState = default)
            : base(context, defaultState) { }
    }
}