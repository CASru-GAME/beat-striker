using System;
using System.Collections.Generic;
using Core.Battle;
using UnityEngine;

namespace Core.Striker {

    public interface IStateContext<in TNode> {
        void TryTransition(TNode node);
    }

    public interface INodeContext<in TNode, in TState> : IStateContext<TNode> {
        void ChangeState(TState state);
    }

    public interface INode<in TNodeContext> {
        void OnTryTransition(TNodeContext context);
    }

    public interface IState<in TContext, TState> where TState : IState<TContext, TState> {
        IEnumerable<IGroup<TContext>> Parents { get; }
        void OnEnter(TContext context);
        void OnExit(TContext context);
    }

    // 親リストを持たない単純なグループ向けのインターフェース
    public interface IGroup<in TContext> {
        void OnEnter(TContext context);
        void OnExit(TContext context);
    }

    public interface IStrikerContext {
        Rigidbody Rigidbody { get; }
        Vector2 InputDirection { get; }
        void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null);
        void ApplyDamage(float damage);
    }

    public interface IStrikerStateContext : IStateContext<IStrikerNode> {
        Rigidbody Rigidbody { get; }
        Vector2 InputDirection { get; }
    }

    public interface IStrikerNodeContext : INodeContext<IStrikerNode, IStrikerState> {
        Vector2 InputDirection { get; }
    }

    public interface IStrikerNode : INode<IStrikerNodeContext> { }

    public interface IStrikerState : IState<IStrikerContext, IStrikerState> {
        void OnUpdate(IStrikerStateContext context);
        void OnHit(IStrikerStateContext context, HitStatus status);
        void OnAttackRequested(IStrikerStateContext context);
        void OnChargeRequested(IStrikerStateContext context);
        void OnGuardRequested(IStrikerStateContext context);
        void OnDashRequested(IStrikerStateContext context);
        void OnMiss(IStrikerStateContext context);
    }

    // 親リストを持たないストライカー用のグループインターフェース
    public interface IStrikerGroup : IGroup<IStrikerContext> {
        void OnUpdate(IStrikerStateContext context);
        void OnHit(IStrikerStateContext context, HitStatus status);
        void OnAttackRequested(IStrikerStateContext context);
        void OnChargeRequested(IStrikerStateContext context);
        void OnGuardRequested(IStrikerStateContext context);
        void OnDashRequested(IStrikerStateContext context);
        void OnMiss(IStrikerStateContext context);
    }
}