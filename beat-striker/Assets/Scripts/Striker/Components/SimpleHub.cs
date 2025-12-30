using UnityEngine;

namespace Core.Striker.Components {

    // インターフェース定義
    public interface ISimpleContext { }
    public interface ISimpleStateContext : IStateContext<ISimpleNode> { }
    public interface ISimpleNodeContext : INodeContext<ISimpleNode, ISimpleState> { }
    public interface ISimpleNode : INode<ISimpleNodeContext> { }
    public interface ISimpleState : IState<ISimpleContext> {
        void OnUpdate(ISimpleStateContext context);
    }

    /// <summary>
    /// Simple専用ステートマシン
    /// </summary>
    public class SimpleHubStateMachine : 
        StateMachine<ISimpleNode, ISimpleState, ISimpleContext, SimpleHubStateMachine>,
        ISimpleStateContext, ISimpleNodeContext
    {
        public SimpleHubStateMachine(ISimpleContext context, ISimpleState defaultState = default)
            : base(context, defaultState) { }
    }

    /// <summary>
    /// シンプルなステートマシンコンポーネント
    /// </summary>
    public class SimpleHub : MonoBehaviour, ISimpleContext {
        [SerializeField] private SimpleState defaultState;

        private SimpleHubStateMachine stateMachine;

        private void Start() {
            stateMachine = new SimpleHubStateMachine(this, defaultState);
        }

        private void Update() {
            stateMachine.CurrentState.OnUpdate(stateMachine);
        }
    }

    /// <summary>
    /// シンプルなノードの基底クラス
    /// </summary>
    public abstract class SimpleNode : MonoBehaviour, ISimpleNode {
        public abstract void OnTryTransition(ISimpleNodeContext context);
    }

    /// <summary>
    /// シンプルなステートの基底クラス
    /// </summary>
    public abstract class SimpleState : SimpleNode, ISimpleState {
        public sealed override void OnTryTransition(ISimpleNodeContext context) {
            context.ChangeState(this);
        }

        public virtual void OnEnter(ISimpleContext context) { }
        public virtual void OnUpdate(ISimpleStateContext context) { }
        public virtual void OnExit(ISimpleContext context) { }
    }
}
