using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Alice {
    [RequireComponent(typeof(BehaviorParameters))]
    public class MLAiDecisionAgent : Agent {
        MLAiBrain brain;
        AiObservation latestObservation;
        bool hasObservation;

        public AiAction CurrentAction { get; private set; } = AiAction.None;

        public void Bind(MLAiBrain brain) {
            this.brain = brain;
        }

        public void SetObservation(AiObservation observation) {
            latestObservation = observation;
            hasObservation = true;
        }

        public void RequestDecisionNow() {
            if (hasObservation) {
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
            hasObservation = false;
        }

        public override void OnEpisodeBegin() {
            CurrentAction = AiAction.None;
            hasObservation = false;
        }

        public override void CollectObservations(VectorSensor sensor) {
            if (brain == null) {
                for (var i = 0; i < MLAiBrain.OBSERVATION_COUNT; i++) {
                    sensor.AddObservation(0f);
                }
                return;
            }

            if (!hasObservation) {
                for (var i = 0; i < MLAiBrain.OBSERVATION_COUNT; i++) {
                    sensor.AddObservation(0f);
                }
                return;
            }

            brain.WriteObservations(sensor, latestObservation);
        }

        public override void OnActionReceived(ActionBuffers actions) {
            if (!hasObservation) {
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
