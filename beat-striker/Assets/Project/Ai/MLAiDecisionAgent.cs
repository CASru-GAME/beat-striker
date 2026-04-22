using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Alice {
    [DefaultExecutionOrder(-999)]
    public class MLAiDecisionAgent : Agent {
        const int OBSERVATION_STACK_COUNT = MLAiBrain.OBSERVATION_STACK_COUNT;

        MLAiBrain brain;
        AiObservation latestObservation;
        readonly AiObservation[] observationHistory = new AiObservation[OBSERVATION_STACK_COUNT];
        int observationHistoryCount;

        public AiAction CurrentAction { get; private set; } = AiAction.None;

        protected override void Awake() {
            base.Awake();
            if (GetComponent<BehaviorParameters>() == null) {
                var behaviorParameters = gameObject.AddComponent<BehaviorParameters>();
                behaviorParameters.hideFlags = HideFlags.HideInInspector;
            }
        }

        public void Bind(MLAiBrain brain) {
            this.brain = brain;
        }

        public void SetObservation(AiObservation observation) {
            latestObservation = observation;

            for (var i = OBSERVATION_STACK_COUNT - 1; i > 0; i--) {
                observationHistory[i] = observationHistory[i - 1];
            }

            observationHistory[0] = observation;
            observationHistoryCount = Mathf.Min(observationHistoryCount + 1, OBSERVATION_STACK_COUNT);
        }

        public void RequestDecisionNow() {
            if (observationHistoryCount > 0) {
                RequestDecision();
            }
        }

        public void AddStepReward(float reward) {
            AddReward(reward);
        }

        public void EndEpisodeWithReward(float reward) {
            AddReward(reward);
            EndEpisode();
            CurrentAction = AiAction.None;
        }

        public void ResetDecisionState() {
            CurrentAction = AiAction.None;
            observationHistoryCount = 0;
        }

        public override void OnEpisodeBegin() {
            CurrentAction = AiAction.None;
            observationHistoryCount = 0;
        }

        public override void CollectObservations(VectorSensor sensor) {
            if (brain == null) {
                for (var i = 0; i < MLAiBrain.STACKED_OBSERVATION_COUNT; i++) {
                    sensor.AddObservation(0f);
                }
                return;
            }

            if (observationHistoryCount == 0) {
                brain.WriteZeroObservations(sensor, MLAiBrain.STACKED_OBSERVATION_COUNT);
                return;
            }

            for (var i = 0; i < OBSERVATION_STACK_COUNT; i++) {
                if (i < observationHistoryCount) {
                    brain.WriteObservations(sensor, observationHistory[i]);
                }
                else {
                    brain.WriteZeroObservations(sensor, MLAiBrain.OBSERVATION_COUNT);
                }
            }
        }

        public override void OnActionReceived(ActionBuffers actions) {
            if (observationHistoryCount == 0) {
                CurrentAction = AiAction.None;
                return;
            }

            CurrentAction = brain.DecodeAction(latestObservation, actions);
        }

        public override void Heuristic(in ActionBuffers actionsOut) {
            if (brain == null) {
                var discreteActions = actionsOut.DiscreteActions;
                discreteActions[0] = 0;
                discreteActions[1] = 0;
                return;
            }

            brain.WriteHeuristic(actionsOut);
        }
    }
}
