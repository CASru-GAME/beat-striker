using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.InferenceEngine;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Alice {
    [RequireComponent(typeof(MLAiDecisionAgent))]
    public class MLAiBrain : AiBrain {
        internal const int STRIKER_TYPE_COUNT = 4;
        internal const int STRIKER_STATE_CATEGORY_COUNT = 7;
        internal const int MOVE_CONTINUOUS_ACTION_SIZE = 2;
        internal const int BUTTON_ACTION_BRANCH_SIZE = 4;
        internal const int SELF_OBSERVATION_COUNT = 17;
        internal const int OPPONENT_OBSERVATION_COUNT = 17;
        internal const int RELATIVE_OBSERVATION_COUNT = 3;
        internal const int ENGINEERED_OBSERVATION_COUNT = 8;
        internal const int OBSERVATION_COUNT = SELF_OBSERVATION_COUNT + OPPONENT_OBSERVATION_COUNT + RELATIVE_OBSERVATION_COUNT + ENGINEERED_OBSERVATION_COUNT;
        internal const int OBSERVATION_STACK_COUNT = 3;
        internal const int STACKED_OBSERVATION_COUNT = OBSERVATION_COUNT * OBSERVATION_STACK_COUNT;

        [Header("ML-Agents Auto Setup")]
        [SerializeField] string behaviorName = "Satan";
        [SerializeField] bool autoConfigureOnAwake = true;
        [SerializeField] bool autoConfigureInEditor = true;

        [Header("ML-Agents Mode")]
        [SerializeField] bool isLearning = true;
        [SerializeField] ModelAsset inferenceOnnx;

        [Header("Distance")]
        [SerializeField] float attackDistance = 1.1f;

        [Header("Reward")]
        [SerializeField] float stepPenalty = -0.001f;
        [SerializeField] float guardSuccessReward = 0.02f;
        [SerializeField] float guardFailedPenalty = -0.05f;
        [SerializeField] float guardUnnecessaryPenalty = -0.015f;
        [SerializeField] float attackWhiffPenalty = -0.02f;
        [SerializeField] float dealtDamageRewardScale = 0.03f;
        [SerializeField] float receivedDamagePenaltyScale = 0.04f;
        [SerializeField] float winReward = 1f;
        [SerializeField] float losePenalty = -1f;

        [Header("Engagement Reward")]
        [SerializeField, Min(1)] int noDamageStreakThresholdSteps = 6;
        [SerializeField] float noDamageStreakPenalty = -0.01f;
        [SerializeField] float noDamageStreakExtraPenaltyPerStep = -0.002f;

        [Header("Movement Reward")]
        [SerializeField, Min(1)] int movementWindowSteps = 6;
        [SerializeField] float minMovementDistanceInWindow = 1.2f;
        [SerializeField] float movementRewardCapDistance = 3f;
        [SerializeField] float movementShortPenalty = -0.01f;
        [SerializeField] float movementLongReward = 0.008f;

        [Header("Credit Assignment")]
        [SerializeField, Min(1)] int actionOutcomeHorizonSteps = 2;

        MLAiDecisionAgent decisionAgent;
        BehaviorParameters behaviorParameters;
        bool hasPreviousHp;
        float previousSelfHp;
        float previousOpponentHp;
        bool hasPreviousSelfPosition;
        Vector3 previousSelfPosition;
        int noDamageStreakSteps;
        float movementDistanceWindowSum;
        readonly Queue<float> movementDistanceWindow = new();
        readonly List<PendingDecision> pendingDecisions = new();
        bool teamIdConfigured;

        class PendingDecision {
            public GamePadButton? Button;
            public StrikerStateCategory OpponentCategoryAtDecision;
            public int RemainingSteps;
            public bool HasDealtDamage;
            public bool HasReceivedDamage;
        }

        void Awake() {
            if (autoConfigureOnAwake) {
                ConfigureMlAgentComponents();
            }

            behaviorParameters = GetComponent<BehaviorParameters>();
            decisionAgent = GetComponent<MLAiDecisionAgent>();
            decisionAgent.Bind(this);
        }

        void OnValidate() {
            if (!autoConfigureInEditor) {
                return;
            }

            ConfigureMlAgentComponents();
        }

        protected override void OnAiEnabled() {
            hasPreviousHp = false;
            hasPreviousSelfPosition = false;
            noDamageStreakSteps = 0;
            movementDistanceWindowSum = 0f;
            movementDistanceWindow.Clear();
            pendingDecisions.Clear();
            teamIdConfigured = false;
            decisionAgent.ResetDecisionState();
        }

        protected override void OnAiDisabled() {
            hasPreviousHp = false;
            hasPreviousSelfPosition = false;
            noDamageStreakSteps = 0;
            movementDistanceWindowSum = 0f;
            movementDistanceWindow.Clear();
            pendingDecisions.Clear();
            teamIdConfigured = false;
        }

        protected override AiAction OnGoodWindow(AiObservation observation) {
            ConfigureTeamId(observation.Self.PlayerId.CurrentValue);
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
            var distance = offset2D.magnitude;
            var directionToOpponent = distance > 0.0001f ? offset2D / distance : Vector2.right;

            var selfHpRate = NormalizeRatio(self.HitPoint.CurrentValue, self.MaxHitPoint.CurrentValue);
            var opponentHpRate = NormalizeRatio(opponent.HitPoint.CurrentValue, opponent.MaxHitPoint.CurrentValue);
            var selfSpRate = NormalizeRatio(self.SpecialPoint.CurrentValue, self.MaxSpecialPoint.CurrentValue);
            var opponentSpRate = NormalizeRatio(opponent.SpecialPoint.CurrentValue, opponent.MaxSpecialPoint.CurrentValue);

            sensor.AddObservation(self.Position.CurrentValue.x);
            sensor.AddObservation(self.Position.CurrentValue.y);
            sensor.AddObservation(self.Velocity.CurrentValue.x);
            sensor.AddObservation(self.Velocity.CurrentValue.y);
            sensor.AddObservation(selfHpRate);
            sensor.AddObservation(selfSpRate);
            WriteStateCategoryOneHot(sensor, self.CurrentStateCategory.CurrentValue);
            WriteStrikerOneHot(sensor, self.Striker.CurrentValue);

            sensor.AddObservation(opponent.Position.CurrentValue.x);
            sensor.AddObservation(opponent.Position.CurrentValue.y);
            sensor.AddObservation(opponent.Velocity.CurrentValue.x);
            sensor.AddObservation(opponent.Velocity.CurrentValue.y);
            sensor.AddObservation(opponentHpRate);
            sensor.AddObservation(opponentSpRate);
            WriteStateCategoryOneHot(sensor, opponent.CurrentStateCategory.CurrentValue);
            WriteStrikerOneHot(sensor, opponent.Striker.CurrentValue);

            sensor.AddObservation(offset2D.x);
            sensor.AddObservation(offset2D.y);
            sensor.AddObservation(distance);

            var relativeVelocity = new Vector2(
                opponent.Velocity.CurrentValue.x - self.Velocity.CurrentValue.x,
                opponent.Velocity.CurrentValue.y - self.Velocity.CurrentValue.y
            );
            var closingSpeed = Vector2.Dot(relativeVelocity, directionToOpponent);
            var selfFacing = Vector3.Dot(self.LookDirection.CurrentValue, offset.normalized);
            var opponentFacing = Vector3.Dot(opponent.LookDirection.CurrentValue, (-offset).normalized);

            sensor.AddObservation(distance / 10f);
            sensor.AddObservation(closingSpeed / 10f);
            sensor.AddObservation(selfFacing);
            sensor.AddObservation(opponentFacing);
            sensor.AddObservation(selfHpRate - opponentHpRate);
            sensor.AddObservation(selfSpRate - opponentSpRate);
            sensor.AddObservation(Mathf.Abs(offset2D.x));
            sensor.AddObservation(Mathf.Abs(offset2D.y));
        }

        internal void WriteZeroObservations(VectorSensor sensor, int count) {
            for (var i = 0; i < count; i++) {
                sensor.AddObservation(0f);
            }
        }

        internal AiAction DecodeAction(AiObservation observation, ActionBuffers actions) {
            var moveX = actions.ContinuousActions[0];
            var moveY = actions.ContinuousActions[1];
            var buttonAction = actions.DiscreteActions[0];

            var moveDirection = DecodeMoveDirection(moveX, moveY, observation.Self, observation.Opponent);
            var button = DecodeButton(buttonAction);
            return new AiAction(moveDirection, button);
        }

        internal void WriteHeuristic(ActionBuffers actionsOut) {
            var continuousActions = actionsOut.ContinuousActions;
            var discreteActions = actionsOut.DiscreteActions;
            continuousActions[0] = 1f;
            continuousActions[1] = 0f;
            discreteActions[0] = 0;

            var x = 0f;
            var y = 0f;

            if (IsPressed(Key.LeftArrow, KeyCode.LeftArrow)) {
                x -= 1f;
            }

            if (IsPressed(Key.RightArrow, KeyCode.RightArrow)) {
                x += 1f;
            }

            if (IsPressed(Key.UpArrow, KeyCode.UpArrow)) {
                y += 1f;
            }

            if (IsPressed(Key.DownArrow, KeyCode.DownArrow)) {
                y -= 1f;
            }

            var direction = new Vector2(x, y);
            if (direction.sqrMagnitude > 0.000001f) {
                direction.Normalize();
                continuousActions[0] = direction.x;
                continuousActions[1] = direction.y;
            }

            if (IsPressed(Key.Z, KeyCode.Z)) {
                discreteActions[0] = 0;
            }

            if (IsPressed(Key.X, KeyCode.X)) {
                discreteActions[0] = 1;
            }

            if (IsPressed(Key.C, KeyCode.C)) {
                discreteActions[0] = 2;
            }

            if (IsPressed(Key.V, KeyCode.V)) {
                discreteActions[0] = 3;
            }
        }

        static bool IsPressed(
#if ENABLE_INPUT_SYSTEM
            Key inputSystemKey,
#else
            object _,
#endif
            KeyCode legacyKeyCode
        ) {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[inputSystemKey].isPressed;
#else
            return Input.GetKey(legacyKeyCode);
#endif
        }

        void EvaluateAndReward(AiObservation observation, AiAction action) {
            var self = observation.Self;
            var opponent = observation.Opponent;
            UpdateMovementDistanceWindow(self.Position.CurrentValue);

            decisionAgent.AddStepReward(stepPenalty);
            EvaluateMovementDistanceReward();

            if (hasPreviousHp) {
                var dealtDamage = Mathf.Max(0f, previousOpponentHp - opponent.HitPoint.CurrentValue);
                var receivedDamage = Mathf.Max(0f, previousSelfHp - self.HitPoint.CurrentValue);
                var totalDamageExchanged = dealtDamage + receivedDamage;

                if (totalDamageExchanged > 0.0001f) {
                    noDamageStreakSteps = 0;
                } else {
                    noDamageStreakSteps++;
                    if (noDamageStreakSteps >= noDamageStreakThresholdSteps) {
                        var extraSteps = noDamageStreakSteps - noDamageStreakThresholdSteps;
                        decisionAgent.AddStepReward(noDamageStreakPenalty + noDamageStreakExtraPenaltyPerStep * extraSteps);
                    }
                }

                UpdatePendingDecisionOutcomes(receivedDamage, dealtDamage);

                if (dealtDamage > 0f) {
                    decisionAgent.AddStepReward(dealtDamage * dealtDamageRewardScale);
                }

                if (receivedDamage > 0f) {
                    decisionAgent.AddStepReward(-receivedDamage * receivedDamagePenaltyScale);
                }
            }

            EnqueuePendingDecision(action.Button, opponent.CurrentStateCategory.CurrentValue);

            previousSelfHp = self.HitPoint.CurrentValue;
            previousOpponentHp = opponent.HitPoint.CurrentValue;
            hasPreviousHp = true;

            if (self.HitPoint.CurrentValue <= 0f) {
                decisionAgent.EndEpisodeWithReward(losePenalty);
                hasPreviousHp = false;
                hasPreviousSelfPosition = false;
                noDamageStreakSteps = 0;
                movementDistanceWindowSum = 0f;
                movementDistanceWindow.Clear();
                pendingDecisions.Clear();
                return;
            }

            if (opponent.HitPoint.CurrentValue <= 0f) {
                decisionAgent.EndEpisodeWithReward(winReward);
                hasPreviousHp = false;
                hasPreviousSelfPosition = false;
                noDamageStreakSteps = 0;
                movementDistanceWindowSum = 0f;
                movementDistanceWindow.Clear();
                pendingDecisions.Clear();
            }
        }

        void UpdateMovementDistanceWindow(Vector3 currentPosition) {
            if (!hasPreviousSelfPosition) {
                previousSelfPosition = currentPosition;
                hasPreviousSelfPosition = true;
                return;
            }

            var stepDistance = Vector3.Distance(previousSelfPosition, currentPosition);
            previousSelfPosition = currentPosition;

            movementDistanceWindow.Enqueue(stepDistance);
            movementDistanceWindowSum += stepDistance;

            while (movementDistanceWindow.Count > movementWindowSteps) {
                movementDistanceWindowSum -= movementDistanceWindow.Dequeue();
            }
        }

        void EvaluateMovementDistanceReward() {
            if (movementDistanceWindow.Count < movementWindowSteps) {
                return;
            }

            if (movementDistanceWindowSum < minMovementDistanceInWindow) {
                var shortRatio = 1f - Mathf.Clamp01(movementDistanceWindowSum / Mathf.Max(0.0001f, minMovementDistanceInWindow));
                decisionAgent.AddStepReward(movementShortPenalty * (1f + shortRatio));
                return;
            }

            var rewardDenominator = Mathf.Max(minMovementDistanceInWindow, movementRewardCapDistance);
            var rewardRatio = Mathf.Clamp01(movementDistanceWindowSum / rewardDenominator);
            decisionAgent.AddStepReward(movementLongReward * rewardRatio);
        }

        void EnqueuePendingDecision(GamePadButton? button, StrikerStateCategory opponentCategory) {
            pendingDecisions.Add(new PendingDecision {
                Button = button,
                OpponentCategoryAtDecision = opponentCategory,
                RemainingSteps = actionOutcomeHorizonSteps,
                HasDealtDamage = false,
                HasReceivedDamage = false,
            });
        }

        void UpdatePendingDecisionOutcomes(float receivedDamage, float dealtDamage) {
            var remainingDealt = dealtDamage;
            var remainingReceived = receivedDamage;

            for (var i = 0; i < pendingDecisions.Count; i++) {
                var pending = pendingDecisions[i];
                if (pending.Button == GamePadButton.East && remainingDealt > 0f) {
                    pending.HasDealtDamage = true;
                    remainingDealt = 0f;
                }

                if (pending.Button == GamePadButton.North && remainingReceived > 0f) {
                    pending.HasReceivedDamage = true;
                    remainingReceived = 0f;
                }

                pending.RemainingSteps--;
            }

            for (var i = pendingDecisions.Count - 1; i >= 0; i--) {
                var pending = pendingDecisions[i];
                if (pending.RemainingSteps > 0) {
                    continue;
                }

                EvaluateFinalizedDecision(pending);
                pendingDecisions.RemoveAt(i);
            }
        }

        void EvaluateFinalizedDecision(PendingDecision pending) {
            if (!pending.Button.HasValue) {
                return;
            }

            var button = pending.Button.Value;
            if (button == GamePadButton.North) {
                var isThreatCategory = pending.OpponentCategoryAtDecision == StrikerStateCategory.Attack
                    || pending.OpponentCategoryAtDecision == StrikerStateCategory.Special;

                if (pending.HasReceivedDamage) {
                    decisionAgent.AddStepReward(guardFailedPenalty);
                } else if (isThreatCategory) {
                    decisionAgent.AddStepReward(guardSuccessReward);
                } else {
                    decisionAgent.AddStepReward(guardUnnecessaryPenalty);
                }
                return;
            }

            if (button == GamePadButton.East && !pending.HasDealtDamage) {
                decisionAgent.AddStepReward(attackWhiffPenalty);
            }
        }

        Vector2 DecodeMoveDirection(float moveX, float moveY, IObservableStriker self, IObservableStriker opponent) {
            var direction = new Vector2(moveX, moveY);
            if (direction.sqrMagnitude > 0.000001f) {
                return direction.normalized;
            }

            var toOpponent = opponent.Position.CurrentValue - self.Position.CurrentValue;
            var fallback = new Vector2(toOpponent.x, toOpponent.y);
            if (fallback.sqrMagnitude > 0.000001f) {
                return fallback.normalized;
            }

            return Vector2.right;
        }

        static GamePadButton? DecodeButton(int buttonAction) {
            return buttonAction switch {
                0 => GamePadButton.East,
                1 => GamePadButton.South,
                2 => GamePadButton.West,
                3 => GamePadButton.North,
                _ => GamePadButton.East,
            };
        }

        static float NormalizeRatio(float current, float max) {
            if (max <= 0f) {
                return 0f;
            }

            return Mathf.Clamp01(current / max);
        }

        static void WriteStrikerOneHot(VectorSensor sensor, Striker striker) {
            var index = (int)striker;
            for (var i = 0; i < STRIKER_TYPE_COUNT; i++) {
                sensor.AddObservation(i == index ? 1f : 0f);
            }
        }

        static void WriteStateCategoryOneHot(VectorSensor sensor, StrikerStateCategory category) {
            var index = (int)category;
            for (var i = 0; i < STRIKER_STATE_CATEGORY_COUNT; i++) {
                sensor.AddObservation(i == index ? 1f : 0f);
            }
        }

        void ConfigureTeamId(int playerId) {
            if (teamIdConfigured) {
                return;
            }

            behaviorParameters.TeamId = playerId & 1;
            teamIdConfigured = true;
        }

        void ConfigureMlAgentComponents() {
            behaviorParameters ??= GetComponent<BehaviorParameters>();
            var brainParameters = behaviorParameters.BrainParameters;

            brainParameters.VectorObservationSize = STACKED_OBSERVATION_COUNT;
            brainParameters.NumStackedVectorObservations = 1;
            brainParameters.ActionSpec = new ActionSpec(MOVE_CONTINUOUS_ACTION_SIZE, new[] { BUTTON_ACTION_BRANCH_SIZE });

            behaviorParameters.BehaviorName = behaviorName;
            if (!isLearning) {
                behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
                behaviorParameters.Model = inferenceOnnx;
            } else {
                behaviorParameters.BehaviorType = BehaviorType.Default;
                behaviorParameters.Model = null;
            }
        }
    }

    // MLAiDecisionAgent moved to MLAiDecisionAgent.cs
}