using System.Collections.Generic;
using UnityEngine;
using Core.Striker;

namespace Core.Striker.Components {

    public interface ISimpleContext { }
    public interface ISimpleStateContext : IStateContext<ISimpleNode> { }
    public interface ISimpleNodeContext : INodeContext<ISimpleNode, ISimpleState> { }
    public interface ISimpleNode : INode<ISimpleNodeContext> { }
    public interface ISimpleState : IState<ISimpleContext, ISimpleState> {
        void OnUpdate(ISimpleStateContext context);
    }

    public class SimpleHubStateMachine : 
        StateMachine<ISimpleNode, ISimpleState, ISimpleContext, SimpleHubStateMachine>,
        ISimpleStateContext, ISimpleNodeContext
    {
        public SimpleHubStateMachine(ISimpleContext context, ISimpleState defaultState = default)
            : base(context, defaultState) { }
    }

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

    public abstract class SimpleNode : MonoBehaviour, ISimpleNode {
        public abstract void OnTryTransition(ISimpleNodeContext context);
    }

    public abstract class SimpleState : SimpleNode, ISimpleState {
        [SerializeField] private List<SimpleGroup> parents = new List<SimpleGroup>();
        public virtual IEnumerable<IGroup<ISimpleContext>> Parents => parents;

        public sealed override void OnTryTransition(ISimpleNodeContext context) {
            context.ChangeState(this);
        }

        public virtual void OnEnter(ISimpleContext context) { }
        public virtual void OnUpdate(ISimpleStateContext context) { }
        public virtual void OnExit(ISimpleContext context) { }
    }

    public abstract class SimpleGroup : MonoBehaviour, IGroup<ISimpleContext> {
        public virtual void OnEnter(ISimpleContext context) { }
        public virtual void OnExit(ISimpleContext context) { }
    }
}
