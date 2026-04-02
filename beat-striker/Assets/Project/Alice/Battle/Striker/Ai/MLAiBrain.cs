using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Alice {
    [RequireComponent(typeof(MLAiDecisionAgent))]
    public class MLAiBrain : AiBrain {
        internal const int SELF_OBSERVATION_COUNT = 5;
        internal const int OPPONENT_OBSERVATION_COUNT = 5;
        internal const int RELATIVE_OBSERVATION_COUNT = 3;
        internal const int OBSERVATION_COUNT = SELF_OBSERVATION_COUNT + OPPONENT_OBSERVATION_COUNT + RELATIVE_OBSERVATION_COUNT;

        [Header("Distance")]
        [SerializeField] float keepDistance = 1.2f;
        [SerializeField] float keepDistanceTolerance = 0.4f;
        [SerializeField] float attackDistance = 1.5f;
        [SerializeField] float jumpDirectionY = 1f;

        [Header("Reward")]
        [SerializeField] float stepPenalty = -0.001f;
        [SerializeField] float spacingReward = 0.01f;
        [SerializeField] float badSpacingPenalty = -0.005f;
        [SerializeField] float validAttackIntentReward = 0.05f;
        [SerializeField] float invalidAttackIntentPenalty = -0.02f;
        [SerializeField] float dealtDamageRewardScale = 0.03f;
        [SerializeField] float receivedDamagePenaltyScale = 0.04f;
        [SerializeField] float winReward = 1f;
        [SerializeField] float losePenalty = -1f;

        MLAiDecisionAgent decisionAgent;
        bool hasPreviousHp;
        float previousSelfHp;
        float previousOpponentHp;

        void Awake() {
            decisionAgent = GetComponent<MLAiDecisionAgent>();
            decisionAgent.Bind(this);
        }

        protected override void OnAiEnabled() {
            hasPreviousHp = false;
            decisionAgent.ResetDecisionState();
        }

        protected override void OnAiDisabled() {
            hasPreviousHp = false;
        }

        protected override AiAction OnGoodWindow(AiObservation observation) {
            decisionAgent.SetObservation(observation);
            decisionAgent.RequestDecisionNow();

            var action = decisionAgent.CurrentAction;
            EvaluateAndReward(observation, action);
            return action;
        }

        internal void WriteObservations(VectorSensor sensor, AiObservation observation) {
            var self = observation.Self;
            var opponent = observation.Opponent;
            var offset = opponent.Position.CurrentValue - self.Position.CurrentValue;
            var offset2D = new Vector2(offset.x, offset.y);

            sensor.AddObservation(self.Position.CurrentValue.x);
            sensor.AddObservation(self.Position.CurrentValue.y);
            sensor.AddObservation(self.Velocity.CurrentValue.x);
            sensor.AddObservation(self.Velocity.CurrentValue.y);
            sensor.AddObservation(self.HitPoint.CurrentValue / self.MaxHitPoint.CurrentValue);

            sensor.AddObservation(opponent.Position.CurrentValue.x);
            sensor.AddObservation(opponent.Position.CurrentValue.y);
            sensor.AddObservation(opponent.Velocity.CurrentValue.x);
            sensor.AddObservation(opponent.Velocity.CurrentValue.y);
            sensor.AddObservation(opponent.HitPoint.CurrentValue / opponent.MaxHitPoint.CurrentValue);

            sensor.AddObservation(offset2D.x);
            sensor.AddObservation(offset2D.y);
            sensor.AddObservation(offset2D.magnitude);

            
        }

        internal AiAction DecodeAction(AiObservation observation, ActionBuffers actions) {
            var moveAction = actions.DiscreteActions[0];
            var buttonAction = actions.DiscreteActions[1];

            var moveDirection = DecodeMoveDirection(moveAction, observation.Self, observation.Opponent);
            var button = DecodeButton(buttonAction);
            return new AiAction(moveDirection, button);
        }

        internal void WriteHeuristic(ActionBuffers actionsOut) {
            var discreteActions = actionsOut.DiscreteActions;
            discreteActions[0] = 0;
            discreteActions[1] = 0;

            if (Input.GetKey(KeyCode.LeftArrow)) {
                discreteActions[0] = 2;
            }

            if (Input.GetKey(KeyCode.RightArrow)) {
                discreteActions[0] = 1;
            }

            if (Input.GetKey(KeyCode.UpArrow)) {
                discreteActions[0] = 3;
            }

            if (Input.GetKey(KeyCode.DownArrow)) {
                discreteActions[0] = 4;
            }

            if (Input.GetKey(KeyCode.Z)) {
                discreteActions[1] = 1;
            }

            if (Input.GetKey(KeyCode.X)) {
                discreteActions[1] = 2;
            }

            if (Input.GetKey(KeyCode.C)) {
                discreteActions[1] = 3;
            }

            if (Input.GetKey(KeyCode.V)) {
                discreteActions[1] = 4;
            }
        }

        void EvaluateAndReward(AiObservation observation, AiAction action) {
            var self = observation.Self;
            var opponent = observation.Opponent;
            var distance = Vector2.Distance(
                new Vector2(self.Position.CurrentValue.x, self.Position.CurrentValue.y),
                new Vector2(opponent.Position.CurrentValue.x, opponent.Position.CurrentValue.y)
            );

            decisionAgent.AddStepReward(stepPenalty);

            var minKeep = keepDistance - keepDistanceTolerance;
            var maxKeep = keepDistance + keepDistanceTolerance;
            if (distance >= minKeep && distance <= maxKeep) {
                decisionAgent.AddStepReward(spacingReward);
            } else {
                decisionAgent.AddStepReward(badSpacingPenalty);
            }

            if (action.Button == GamePadButton.East) {
                if (distance <= attackDistance) {
                    decisionAgent.AddStepReward(validAttackIntentReward);
                } else {
                    decisionAgent.AddStepReward(invalidAttackIntentPenalty);
                }
            }

            if (hasPreviousHp) {
                var dealtDamage = Mathf.Max(0f, previousOpponentHp - opponent.HitPoint.CurrentValue);
                var receivedDamage = Mathf.Max(0f, previousSelfHp - self.HitPoint.CurrentValue);

                if (dealtDamage > 0f) {
                    decisionAgent.AddStepReward(dealtDamage * dealtDamageRewardScale);
                }

                if (receivedDamage > 0f) {
                    decisionAgent.AddStepReward(-receivedDamage * receivedDamagePenaltyScale);
                }
            }

            previousSelfHp = self.HitPoint.CurrentValue;
            previousOpponentHp = opponent.HitPoint.CurrentValue;
            hasPreviousHp = true;

            if (self.HitPoint.CurrentValue <= 0f) {
                decisionAgent.EndEpisodeWithReward(losePenalty);
                hasPreviousHp = false;
                return;
            }

            if (opponent.HitPoint.CurrentValue <= 0f) {
                decisionAgent.EndEpisodeWithReward(winReward);
                hasPreviousHp = false;
            }
        }

        Vector2 DecodeMoveDirection(int moveAction, IObservableStriker self, IObservableStriker opponent) {
            var toOpponent = (opponent.Position.CurrentValue - self.Position.CurrentValue);
            var horizontal = Mathf.Sign(toOpponent.x);
            if (horizontal == 0f) {
                horizontal = 1f;
            }

            return moveAction switch {
                1 => new Vector2(horizontal, 0f),
                2 => new Vector2(-horizontal, 0f),
                3 => new Vector2(horizontal, jumpDirectionY).normalized,
                4 => new Vector2(-horizontal, jumpDirectionY).normalized,
                _ => Vector2.zero,
            };
        }

        static GamePadButton? DecodeButton(int buttonAction) {
            return buttonAction switch {
                1 => GamePadButton.East,
                2 => GamePadButton.South,
                3 => GamePadButton.Right,
                4 => GamePadButton.West,
                _ => null,
            };
        }
    }

    // MLAiDecisionAgent moved to MLAiDecisionAgent.cs
}