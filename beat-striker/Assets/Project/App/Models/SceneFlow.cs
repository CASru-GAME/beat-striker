using System;
using System.Collections.Generic;
using Core;
using R3;

/**namespace App {
    public class SceneFlow {
        readonly IConsensusStateMachine stateMachine = new ConsensusStateMachine();
        public IOwnableConsensusState TransitionInState { get; private set; } = new ConsensusState();
        public IOwnableConsensusState MainState { get; private set; } = new ConsensusState();
        public IOwnableConsensusState TransitionOutState { get; private set; } = new ConsensusState();

        public SceneFlow(GlobalSceneFlow sceneFlowHub) {
            sceneFlowHub.TransitionInState.OnEnter.Subscribe(_ => stateMachine.RequestChangeState(TransitionInState));
            sceneFlowHub.MainState.OnEnter.Subscribe(_ => stateMachine.RequestChangeState(MainState));
            sceneFlowHub.TransitionOutState.OnEnter.Subscribe(_ => stateMachine.RequestChangeState(TransitionOutState));
        }
    }

    public class SceneTransitionFlow {
        readonly IConsensusStateMachine stateMachine = new ConsensusStateMachine();
        public IOwnableConsensusState TransitionOutState { get; private set; } = new ConsensusState();
        public IOwnableConsensusState SceneLoadState { get; private set; } = new ConsensusState();
        public IOwnableConsensusState TransitionInState { get; private set; } = new ConsensusState();

        public SceneTransitionFlow(GlobalSceneFlow sceneFlowHub) {
            sceneFlowHub.TransitionOutState.OnEnter.Subscribe(_ => stateMachine.RequestChangeState(TransitionOutState));
            sceneFlowHub.SceneLoadState.OnEnter.Subscribe(_ => stateMachine.RequestChangeState(SceneLoadState));
            sceneFlowHub.NextTransitionInState.OnEnter.Subscribe(_ => stateMachine.RequestChangeState(TransitionInState));
        }
    }

    public class GlobalSceneFlow {
        readonly IConsensusStateMachine stateMachine = new ConsensusStateMachine();
        public IOwnableConsensusState TransitionInState { get; private set; }
        public IOwnableConsensusState MainState { get; private set; }
        public IOwnableConsensusState TransitionOutState { get; private set; }
        public IOwnableConsensusState SceneLoadState { get; private set; } 
        public IOwnableConsensusState NextTransitionInState { get; private set; }

        public GlobalSceneFlow() {

        }


        public void RequestSceneTransition(SceneTransitionRequest request) {
            stateMachine.RequestChangeState();
        }
    }
}**/