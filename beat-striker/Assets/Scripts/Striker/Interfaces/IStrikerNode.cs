using System;
using Core.Battle;
using UnityEngine;

namespace Core.Striker {

    // ===========================================
    // 汎用ステートマシン基盤インターフェース
    // ===========================================

    /// <summary>
    /// ノードへの遷移を試行するコンテキスト
    /// </summary>
    public interface IStateContext<in TNode> {
        void TryTransition(TNode node);
    }

    /// <summary>
    /// ステートを直接変更できるコンテキスト（Node用）
    /// </summary>
    public interface INodeContext<in TNode, in TState> : IStateContext<TNode> {
        void ChangeState(TState state);
    }

    /// <summary>
    /// 遷移時に評価されるノード
    /// </summary>
    public interface INode<in TNodeContext> {
        void OnTryTransition(TNodeContext context);
    }

    /// <summary>
    /// ステートのライフサイクル
    /// </summary>
    public interface IState<in TContext> {
        void OnEnter(TContext context);
        void OnExit(TContext context);
    }

    // ===========================================
    // Striker専用インターフェース
    // ===========================================

    /// <summary>
    /// StrikerのOnEnter/OnExitで使用されるコンテキスト
    /// アニメーション再生やRigidbodyアクセスが可能
    /// </summary>
    public interface IStrikerContext {
        Rigidbody Rigidbody { get; }
        Vector2 InputDirection { get; }
        void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null);
        void ApplyDamage(HitPoint damage);
    }

    /// <summary>
    /// StrikerのOnUpdate/コマンド系で使用されるコンテキスト
    /// 遷移要求とRigidbody/InputDirectionへのアクセスが可能
    /// </summary>
    public interface IStrikerStateContext : IStateContext<IStrikerNode> {
        Rigidbody Rigidbody { get; }
        Vector2 InputDirection { get; }
    }

    /// <summary>
    /// StrikerNodeのOnTryTransitionで使用されるコンテキスト
    /// ステート変更と遷移要求、InputDirectionへのアクセスが可能
    /// </summary>
    public interface IStrikerNodeContext : INodeContext<IStrikerNode, IStrikerState> {
        Vector2 InputDirection { get; }
    }

    /// <summary>
    /// StrikerノードのベースインターフェースS
    /// </summary>
    public interface IStrikerNode : INode<IStrikerNodeContext> { }

    /// <summary>
    /// Strikerステートのベースインターフェース
    /// </summary>
    public interface IStrikerState : IState<IStrikerContext> {
        void OnUpdate(IStrikerStateContext context);
        void OnHit(IStrikerStateContext context, HitStatus status);
        void OnAttackRequested(IStrikerStateContext context);
        void OnChargeRequested(IStrikerStateContext context);
        void OnGuardRequested(IStrikerStateContext context);
        void OnDashRequested(IStrikerStateContext context);
        void OnMiss(IStrikerStateContext context);
    }
}