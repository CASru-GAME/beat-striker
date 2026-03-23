using System;
using System.Collections.Generic;
using Core;
using R3;
using UnityEngine;

namespace App {
    public class SceneFlow : MonoBehaviour {
        readonly IConsensusStateMachine stateMachine = new ConsensusStateMachine();
        public IOwnableConsensusState TransitionInState { get; private set; } = new ConsensusState();
        public IOwnableConsensusState MainState { get; private set; } = new ConsensusState();
        public IOwnableConsensusState TransitionOutState { get; private set; } = new ConsensusState();

        public SceneFlow(GlobalSceneFlow sceneFlowHub) {
            var transitionInState = sceneFlowHub.TransitionInState.Own();
            TransitionInState.OnExit.Subscribe(_ => transitionInState.Release()).AddTo(this);
            sceneFlowHub.MainState.Own();
            sceneFlowHub.TransitionOutState.Own();
        }

        void Start() {
            stateMachine.RequestChangeState();
        }
    }

    public class SceneTransitionFlow {
        readonly IConsensusStateMachine stateMachine = new ConsensusStateMachine();
        public IOwnableConsensusState TransitionOutState { get; private set; } = new ConsensusState();
        public IOwnableConsensusState SceneLoadState { get; private set; } = new ConsensusState();
        public IOwnableConsensusState TransitionInState { get; private set; } = new ConsensusState();

        public SceneTransitionFlow(GlobalSceneFlow sceneFlowHub) {
            sceneFlowHub.TransitionOutState.Own();
            sceneFlowHub.SceneLoadState.Own();
            sceneFlowHub.NextTransitionInState.Own();
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
}